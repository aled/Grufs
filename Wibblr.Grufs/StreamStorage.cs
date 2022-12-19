using System;
using System.Runtime.CompilerServices;

using Wibblr.Grufs.Encryption;

[assembly: InternalsVisibleTo("Wibblr.Grufs.Tests")]

namespace Wibblr.Grufs
{
    public class StreamStorage
    {
        private ChunkEncryptor _chunkEncryptor = new ChunkEncryptor();
        private IChunkStorage _chunkStorage;
        private int _chunkSize;

        public StreamStorage(IChunkStorage chunkStorage, int chunkSize = 128 * 1024)
        {
            if (chunkSize < 128)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize));
            }

            _chunkStorage = chunkStorage;
            _chunkSize = chunkSize;
        }

        public (Address, ChunkType) Write(KeyEncryptionKey contentKeyEncryptionKey, WrappedHmacKey wrappedHmacKey, HmacKeyEncryptionKey hmacKeyEncryptionKey, Stream stream)
        {
            var chains = new List<Chain>(); // one for each level of the tree of chains

            // Chain buffer has 32 bytes header, then each contained address is 32 bytes plus 8 for the contained length.
            var chainCapacity = (_chunkSize - Chain.headerLength) / Chain.itemLength;
            if (chainCapacity < 2)
            {
                throw new Exception("Invalid chain capacity");
            }

            var hmacKey = new HmacKey(hmacKeyEncryptionKey, wrappedHmacKey);

            void AppendToChain(Address address, int level, long streamOffset, long streamLength)
            {
                if (level > byte.MaxValue)
                {
                    throw new Exception("Invalid level (infinite loop?)");
                }

                if (chains.Count <= level)
                {
                    chains.Add(new Chain(chainCapacity, level));
                }

                var chain = chains[level];
                if (chain.IsFull())
                {
                    WriteChain(chain);
                    chain.Clear();
                }

                chain.Append(address, streamOffset, streamLength);
            }

            Address WriteChain(Chain chain)
            {
                var content = chain.Serialize();
                var chainChunk = _chunkEncryptor.EncryptChunk(contentKeyEncryptionKey, hmacKey, content);

                if (!_chunkStorage.TryPut(chainChunk, OverwriteStrategy.DenyWithSuccess))
                {
                    throw new Exception("Failed to store chunk in repository");
                }

                Console.WriteLine($"Wrote chain chunk (level {chain.Level}, count {chain.Count}, streamoffset {chain.StreamOffset}, streamlength {chain.StreamLength})");
                AppendToChain(chainChunk.Address, chain.Level + 1, chain.StreamOffset, chain.StreamLength);
                return chainChunk.Address;
            }

            void Write(ReadOnlySpan<byte> bytes, long streamOffset)
            {
                var encryptedChunk = _chunkEncryptor.EncryptChunk(contentKeyEncryptionKey, hmacKey, bytes);

                if (!_chunkStorage.TryPut(encryptedChunk, OverwriteStrategy.DenyWithSuccess))
                {
                    throw new Exception("Failed to store chunk in repository");
                }
                Console.WriteLine($"Wrote content chunk (offset {streamOffset}, length {bytes.Length})");
                AppendToChain(encryptedChunk.Address, level: 0, streamOffset, bytes.Length);
            }

            var buf = new byte[_chunkSize];
            var bytesRead = 0;
            var bufContentLength = 0;
            var streamOffset = 0L;

            while ((bytesRead = stream.Read(buf, bufContentLength, _chunkSize - bufContentLength)) != 0)
            {
                bufContentLength += bytesRead;
                if (_chunkSize == bufContentLength)
                {
                    Write(buf, streamOffset);
                    streamOffset += bufContentLength;
                    bufContentLength = 0;
                }
            }

            if (bufContentLength > 0)
            {
                Write(buf.AsSpan(0, bufContentLength), streamOffset);
            }

            var chain = chains[0];
            var address = chain.Addresses[0];
            if (chains.Count == 1 && chains[0].Count == 1)
            {
                return (address, ChunkType.Content);
            }

            for (int i = 0; i < chains.Count; i++)
            {
                chain = chains[i];

                // Write this chain, unless it is the last chain, and it contains only one chunk
                if (i < chains.Count - 1 || chain.Count > 1)
                {
                    address = WriteChain(chain);
                }
            }

            return (address, ChunkType.Chain);
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
        public IEnumerable<Buffer> Read(ChunkType type, KeyEncryptionKey contentKeyEncryptionKey, HmacKey hmacKey, Address address)
        {
            if (!_chunkStorage.TryGet(address, out var chunk))
            {
                throw new Exception($"Address {address} not found in repository");
            }

            var buffer = _chunkEncryptor.DecryptChunkAndVerifyAddress(chunk, contentKeyEncryptionKey, hmacKey);

            if (type == ChunkType.Content)
            {
                yield return buffer;
            }
            else if (type == ChunkType.Chain)
            {
                var chain = Chain.Deserialize(buffer);

                foreach (var subAddress in chain.Addresses) 
                {
                    foreach (var subBuffer in Read(chain.SubchunkType, contentKeyEncryptionKey, hmacKey, subAddress))
                    {
                        yield return subBuffer;
                    }
                }
            }
            else throw new Exception("Unknown chunk type");
        }
    }
}
