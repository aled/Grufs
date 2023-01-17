using System;
using System.Runtime.CompilerServices;

using Wibblr.Grufs.Core;

[assembly: InternalsVisibleTo("Wibblr.Grufs.Tests")]

namespace Wibblr.Grufs
{
    public class StreamStorage
    {
        private IChunkStorage _chunkStorage;
        private IChunkSourceFactory _chunkSourceFactory;
        private ChunkEncryptor _chunkEncryptor;

        public StreamStorage(IChunkStorage chunkStorage, IChunkSourceFactory chunkSourceFactory, ChunkEncryptor chunkEncryptor)
        {
            _chunkStorage = chunkStorage;
            _chunkSourceFactory = chunkSourceFactory;
            _chunkEncryptor = chunkEncryptor;
        }

        public (Address, int) Write(Stream stream)
        {
            var byteSource = new StreamByteSource(stream);
            var chunkSource = _chunkSourceFactory.Create(byteSource);

            var addresses = new List<(AddressQueueByteSource x, IChunkSource addressChunkSource)>();

            return Write(chunkSource, addresses, 0);
        }

        private (Address, int) Write(IChunkSource chunkSource, List<(AddressQueueByteSource x, IChunkSource addressChunkSource)> addresses, int level)
        {
            if (!chunkSource.Available())
            {
                throw new Exception();
            }

            AddressQueueByteSource addressQueueByteSource;
            IChunkSource addressChunkSource;
            if (addresses.Count <= level)
            {
                addressQueueByteSource = new AddressQueueByteSource(level + 1);
                addressChunkSource = _chunkSourceFactory.Create(addressQueueByteSource);
                addresses.Add((addressQueueByteSource, addressChunkSource));
            }
            else
            {
                (addressQueueByteSource, addressChunkSource) = addresses[level];
            }

            var returnLevel = level;

            // When writing chunks, recursively write to a queue containing the addresses of all the chunks
            // in the stream. (That stream will also have another level of chunk addresses)
            Address address;
            Address? addressStreamAddress = null;

            while (chunkSource.Available())
            {
                var (buf, streamOffset, len) = chunkSource.Next();
                var bytes = buf.AsSpan(0, len);
                var encryptedChunk = _chunkEncryptor.EncryptContentAddressedChunk(bytes);
                if (!_chunkStorage.TryPut(encryptedChunk, OverwriteStrategy.DenyWithSuccess))
                {
                    throw new Exception("Failed to store chunk in repository");
                }
                Console.WriteLine($"Wrote chunk, level {level}, offset {streamOffset}, length {bytes.Length}, address {encryptedChunk.Address}");
                //Console.WriteLine(level == 0 ? Encoding.ASCII.GetString(bytes) : $"   {Convert.ToHexString(bytes)}");
                //Console.WriteLine("-----------------");

                address = encryptedChunk.Address;
                addressQueueByteSource.Add(address, streamOffset, len);

                if (addressChunkSource.Available())
                {
                    (addressStreamAddress, returnLevel) = Write(addressChunkSource, addresses, level + 1);
                }
            }

            if (chunkSource.IsCompleted())
            {
                addressQueueByteSource.CompleteAdding();

                // only write the address chunk if there was more than one address written to it.
                if (addressChunkSource.Available() && addressQueueByteSource.TotalAddressCount > 1)
                {
                    (addressStreamAddress, returnLevel) = Write(addressChunkSource, addresses, level + 1);
                }
            }

            //Console.WriteLine($"Returning from Write: address = {addressStreamAddress ?? address}, level = {returnLevel}");

            return (addressStreamAddress ?? address, returnLevel);
        }

        /// <summary>
        /// TODO: implement a custom stream class that will allow random access without storing the entire stream in memory
        /// </summary>
        /// <param name="type"></param>
        /// <param name="contentKeyEncryptionKey"></param>
        /// <param name="hmacKey"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public IEnumerable<Buffer> Read(int level, Address address)
        {
            var queueIsNew = new List<bool>();
            return Read(level, address, new List<BufferBuilder>(), queueIsNew);
        }

        private IEnumerable<Buffer> Read(int level, Address address, List<BufferBuilder> partialAddressBuilders, List<bool> partialAddressBuilderIsNew)
        {
            if (!_chunkStorage.TryGet(address, out var chunk))
            {
                throw new Exception($"Address {address} not found in repository");
            }

            var buffer = _chunkEncryptor.DecryptContentAddressedChunk(chunk);
            Console.WriteLine($"Read chunk; level = {level} address = {address}");

            if (level == 0)
            {
                yield return buffer;
            }
            else
            {
                while (partialAddressBuilders.Count < level)
                {
                    partialAddressBuilderIsNew.Add(true);
                    partialAddressBuilders.Add(new BufferBuilder(Address.Length + 4));
                }

                var reader = new BufferReader(buffer);

                if (partialAddressBuilderIsNew[level - 1])
                {
                    var serializationVersion = reader.ReadByte(); // serialization version

                    if (serializationVersion != 0)
                    {
                        throw new Exception();
                    }

                    var actualLevel = reader.ReadByte(); // level

                    if (actualLevel != level)
                    {
                        throw new Exception();
                    }

                    //Console.WriteLine($"  serialization version = {serializationVersion}, actualLevel = {actualLevel}");
                    partialAddressBuilderIsNew[level - 1] = false;
                }

                var partialAddressBuilder = partialAddressBuilders[level - 1];
                while (reader.RemainingLength() > 0)
                {
                    int bytesToCopy = Math.Min(reader.RemainingLength(), partialAddressBuilder.RemainingLength());
                    partialAddressBuilder.AppendBytes(reader.ReadBytes(bytesToCopy));

                    if (partialAddressBuilder.RemainingLength() == 0)
                    {
                        var subAddress = new Address(partialAddressBuilder.GetUnderlyingArray().AsSpan(0, Address.Length));
                        var chunkLength = partialAddressBuilder.GetUnderlyingArray().AsSpan(Address.Length, 4);

                        partialAddressBuilder.Clear();

                        var subchunkLevel = level - 1;

                        //Console.WriteLine($"  Read subchunk addresses for level {subchunkLevel}: {subAddress}");

                        foreach (var subBuffer in Read(subchunkLevel, subAddress, partialAddressBuilders, partialAddressBuilderIsNew))
                        {
                            yield return subBuffer;
                        }
                    }
                }

                //Console.WriteLine($"  Level {level}: {queue.Count} bytes remain in queue");
            }
        }
    }
}
