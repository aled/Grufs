using System;
using System.Diagnostics;
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
            IEnumerable<Buffer> Chunks(Stream stream, int chunkSize)
            {
                var buf = new Buffer(chunkSize);
                var bytesRead = 0;

                while ((bytesRead = stream.Read(buf.Bytes, buf.ContentLength, buf.Capacity - buf.ContentLength)) != 0)
                {
                    buf.ContentLength += bytesRead;
                    if (buf.Capacity == buf.ContentLength)
                    {
                        yield return buf;
                        buf.ContentLength = 0;
                    }
                }

                if (buf.ContentLength > 0)
                {
                    yield return buf;
                }
            }

            return WriteChunks(contentKeyEncryptionKey, wrappedHmacKey, hmacKeyEncryptionKey, Chunks(stream, _chunkSize));
        }

        private (Address, ChunkType) WriteChunks(KeyEncryptionKey contentKeyEncryptionKey, WrappedHmacKey wrappedHmacKey, HmacKeyEncryptionKey hmacKeyEncryptionKey, IEnumerable<Buffer> buffers)
        {
            var chainBuffers = new List<Buffer> { new Buffer(_chunkSize) }; // one for each level of the tree of chains
            var hmacKey = new HmacKey(hmacKeyEncryptionKey, wrappedHmacKey);
            var totalAddressesStoredInChainBuffers = 0;

            void AppendToChainBuffer(Address address, int level)
            {
                totalAddressesStoredInChainBuffers++;

                var spaceRequired = Address.Length;

                if (spaceRequired > _chunkSize)
                {
                    throw new ArgumentException(nameof(_chunkSize));
                }

                if (chainBuffers.Count <= level)
                {
                    chainBuffers.Add(new Buffer(_chunkSize));
                }

                var spaceAvailable = chainBuffers[level].Capacity - chainBuffers[level].ContentLength;
                if (spaceAvailable < spaceRequired)
                {
                    WriteChainBuffer(level);
                    chainBuffers[level] = new Buffer(_chunkSize);
                }

                if (chainBuffers[level].ContentLength == 0)
                {
                    // header to identify chain chunks against known-format content chunks
                    chainBuffers[level].Append((byte)'g');
                    chainBuffers[level].Append((byte)'f');
                    chainBuffers[level].Append((byte)'c');
                    chainBuffers[level].Append((byte)255);

                    // serialization version
                    chainBuffers[level].Append((byte)0);

                    chainBuffers[level].Append((byte)level);
                    chainBuffers[level].Append((byte)(level == 0 ? ChunkType.Content : ChunkType.Chain));
                    chainBuffers[level].Append(new byte[25]);
                }

                chainBuffers[level].Append(address.ToSpan());
            }

            void WriteChainBuffer(int level)
            {
                Debug.Assert(chainBuffers.Count > level);

                if (chainBuffers[level].ContentLength == 0)
                {
                    return;
                }

                var chainChunk = _chunkEncryptor.EncryptChunk(InitializationVector.Random(), EncryptionKey.Random(), contentKeyEncryptionKey, hmacKey, chainBuffers[level]);
                if (!_chunkStorage.TryPut(chainChunk, OverwriteStrategy.Allow))
                {
                    throw new Exception("Failed to store chunk in repository");
                }

                AppendToChainBuffer(chainChunk.Address, level + 1);
            }

            Address WriteLastChainBuffer()
            {
                var chainChunk = _chunkEncryptor.EncryptChunk(InitializationVector.Random(), EncryptionKey.Random(), contentKeyEncryptionKey, hmacKey, chainBuffers[chainBuffers.Count - 1]);
                if (!_chunkStorage.TryPut(chainChunk, OverwriteStrategy.Allow))
                {
                    throw new Exception("Failed to store chunk in repository");
                }
                return chainChunk.Address;
            }

            // Use a different (random) IV and encryption key for each chunk
            foreach (var buffer in buffers)
            {
                var encryptedChunk = _chunkEncryptor.EncryptChunk(InitializationVector.Random(), EncryptionKey.Random(), contentKeyEncryptionKey, hmacKey, buffer);

                if (!_chunkStorage.TryPut(encryptedChunk, OverwriteStrategy.Allow))
                {
                    throw new Exception("Failed to store chunk in repository");
                }

                AppendToChainBuffer(encryptedChunk.Address, level: 0);
            }

            if (totalAddressesStoredInChainBuffers == 1)
            {
                return (new Address(chainBuffers[0].ToSpan(32, Address.Length)), ChunkType.Content);
            }

            // write all chain buffers to repository.
            for (int i = 0; i < chainBuffers.Count - 1; i++)
            {
                WriteChainBuffer(i);
            }

            return (WriteLastChainBuffer(), ChunkType.Chain);
        }

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
                ChunkType subchunkType = ChunkType.Unknown;

                if (buffer[0] != 'g' || buffer[1] != 'f' || buffer[2] != 'c' || buffer[3] != 255)
                {
                    throw new Exception();
                }

                if (buffer[4] != 0)
                {
                    // unknown version number
                    throw new Exception();
                }

                // buffer[5] is the chain level - unused here, but might be useful for debugging

                subchunkType = (ChunkType)buffer[6];
                if (subchunkType != ChunkType.Chain && subchunkType != ChunkType.Content)
                {
                    throw new Exception($"Unknown chunk type: {subchunkType}");
                }

                for (int i = 32; i < buffer.ContentLength; i += Address.Length)
                {
                    var subchunkAddress = new Address(buffer.ToSpan(i, Address.Length));

                    foreach (var x in Read(subchunkType, contentKeyEncryptionKey, hmacKey, subchunkAddress))
                    {
                        yield return x;
                    }
                }
            }
            else throw new Exception();
        }
    }
}
