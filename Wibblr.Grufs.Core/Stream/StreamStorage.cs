using System;
using System.Runtime.CompilerServices;

using Wibblr.Grufs.Storage;

[assembly: InternalsVisibleTo("Wibblr.Grufs.Tests")]

namespace Wibblr.Grufs.Core
{
    public record StreamWriteResult(Address address, byte indexLevel, StreamWriteStats stats);

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

        public StreamWriteResult Write(Stream stream)
        {
            var byteSource = new StreamByteSource(stream);
            var chunkSource = _chunkSourceFactory.Create(byteSource);
            var indexByteSources = new List<IndexByteSource>();
            var indexChunkSources = new List<IChunkSource>();
            var stats = new StreamWriteStats();

            var (address, level) = Write(chunkSource, level: 0);
            return new StreamWriteResult(address, level, stats);

            (Address, byte) Write(IChunkSource chunkSource, byte level)
            {
                // When writing chunks, write to an index containing the addresses of all the chunks.
                // (The index will itself be recursively indexed, if it is more than one chunk in length)
                if (level > 100)
                {
                    throw new Exception();
                }

                IndexByteSource index;
                IChunkSource indexChunkSource;
                byte returnLevel = level;
                byte indexLevel = (byte)(level + 1);

                Address address;
                Address? indexAddress = null;
                do
                {
                    // Get next chunk if available. If not available, this must be a zero-length stream.
                    var (buf, streamOffset, len) = chunkSource.Available() 
                        ? chunkSource.Next() 
                        : (new byte[0], 0, 0);

                    var bytes = buf.AsSpan(0, len);
                    var encryptedChunk = _chunkEncryptor.EncryptContentAddressedChunk(bytes);
                    
                    switch (_chunkStorage.Put(encryptedChunk, OverwriteStrategy.Deny))
                    {
                        case PutStatus.Success:
                            if (level == 0)
                            {
                                stats.PlaintextLength += bytes.Length;
                                stats.TransferredContentChunks++;
                                stats.TransferredContentBytes += encryptedChunk.Content.LongLength;
                                stats.TotalContentChunks++;
                                stats.TotalContentBytes += encryptedChunk.Content.LongLength;
                            }
                            else
                            {
                                stats.TransferredIndexChunks++;
                                stats.TransferredIndexBytes += encryptedChunk.Content.LongLength;
                                stats.TotalIndexChunks++;
                                stats.TotalIndexBytes += encryptedChunk.Content.LongLength;
                            }
                            break;

                        case PutStatus.OverwriteDenied:
                            if (level == 0)
                            {
                                stats.PlaintextLength += len;
                                stats.TotalContentChunks++;
                                stats.TotalContentBytes += encryptedChunk.Content.LongLength;
                            }
                            else
                            {
                                stats.TotalIndexChunks++;
                                stats.TotalIndexBytes += encryptedChunk.Content.LongLength;
                            }
                            break;

                        default:
                            throw new Exception("Failed to store chunk in repository");
                    }

                    Console.CursorVisible = false;
                    Console.Write(stats);
                    Console.CursorLeft = 0;
                    //Console.WriteLine($"Wrote chunk, level {level}, offset {streamOffset}, length {bytes.Length}, compressed/encrypted length {encryptedChunk.Content.Length}, address {encryptedChunk.Address}");
                    //Console.WriteLine(level == 0 ? Encoding.ASCII.GetString(bytes) : $"   {Convert.ToHexString(bytes)}");
                    //Console.WriteLine("-----------------");

                    address = encryptedChunk.Address;

                    if (indexByteSources.Count <= level)
                    {
                        index = new IndexByteSource(level + 1);
                        indexChunkSource = _chunkSourceFactory.Create(index);
                        indexByteSources.Add(index);
                        indexChunkSources.Add(indexChunkSource);
                    }
                    else
                    {
                        index = indexByteSources[level];
                        indexChunkSource = indexChunkSources[level];
                    }

                    index.Add(address, streamOffset, len);

                    if (indexChunkSource.Available())
                    {
                        (indexAddress, returnLevel) = Write(indexChunkSource, indexLevel);
                    }
                } while (chunkSource.Available());

                if (chunkSource.IsCompleted())
                {
                    index.CompleteAdding();

                    // only write the address chunk if there was more than one address written to it.
                    if (indexChunkSource.Available() && index.TotalAddressCount > 1)
                    {
                        (indexAddress, returnLevel) = Write(indexChunkSource, indexLevel);
                    }
                }

                //Console.WriteLine($"Returning from Write: address = {addressStreamAddress ?? address}, level = {returnLevel}");

                return (indexAddress ?? address, returnLevel);
            }
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
        public IEnumerable<ArrayBuffer> Read(int level, Address address)
        {
            return Read(level, address, new BufferBuilder[level]);
        }

        private IEnumerable<ArrayBuffer> Read(int level, Address address, BufferBuilder[] indexBuilders)
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
                var reader = new BufferReader(buffer);

                if (indexBuilders[level - 1] == null)
                {
                    indexBuilders[level - 1] = new BufferBuilder(Address.Length + 4);
                
                    var serializationVersion = reader.ReadByte(); // serialization version

                    if (serializationVersion != 0)
                    {
                        throw new Exception();
                    }

                    var deserializedLevel = reader.ReadByte(); // level

                    if (deserializedLevel != level)
                    {
                        throw new Exception();
                    }
                    //Console.WriteLine($"  serialization version = {serializationVersion}, deserializedLevel = {deserializedLevel}");
                }

                var indexBuilder = indexBuilders[level - 1];
                while (reader.RemainingLength() > 0)
                {
                    int bytesToCopy = Math.Min(reader.RemainingLength(), indexBuilder.RemainingLength());
                    indexBuilder.AppendBytes(reader.ReadBytes(bytesToCopy));

                    if (indexBuilder.RemainingLength() == 0)
                    {
                        var subAddress = new Address(indexBuilder.GetUnderlyingArray().AsSpan(0, Address.Length));
                        var chunkLength = indexBuilder.GetUnderlyingArray().AsSpan(Address.Length, 4);

                        indexBuilder.Clear();

                        var subchunkLevel = level - 1;

                        //Console.WriteLine($"  Read subchunk addresses for level {subchunkLevel}: {subAddress}");

                        foreach (var subBuffer in Read(subchunkLevel, subAddress, indexBuilders))
                        {
                            yield return subBuffer;
                        }
                    }
                }
            }
        }
    }
}
