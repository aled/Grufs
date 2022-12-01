using System;
using System.Runtime.CompilerServices;

using Wibblr.Grufs.Encryption;

[assembly: InternalsVisibleTo("Wibblr.Grufs.Tests")]

namespace Wibblr.Grufs
{
    public class StreamEncryptor
    {
        private ChunkEncryptor _chunkEncryptor = new ChunkEncryptor();

        public (Address, ChunkType) EncryptStream(KeyEncryptionKey contentKeyEncryptionKey, WrappedHmacKey wrappedHmacKey, HmacKeyEncryptionKey hmacKeyEncryptionKey, Stream stream, IChunkRepository repository, int chunkSize = 1024 * 1024)
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

            if (chunkSize < 128)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize));
            }

            return EncryptChunks(contentKeyEncryptionKey, wrappedHmacKey, hmacKeyEncryptionKey, Chunks(stream, chunkSize), repository, chunkSize);
        }

        private (Address, ChunkType) EncryptChunks(KeyEncryptionKey contentKeyEncryptionKey, WrappedHmacKey wrappedHmacKey, HmacKeyEncryptionKey hmacKeyEncryptionKey, IEnumerable<Buffer> buffers, IChunkRepository repository, int chunkSize)
        {
            var chainBuffers = new List<Buffer> { new Buffer(chunkSize) }; // one for each level of the tree of chains
            var hmacKey = new HmacKey(hmacKeyEncryptionKey, wrappedHmacKey);

            void AppendToChainBuffer(Address address, int level)
            {
                var spaceRequired = Address.Length;

                if (spaceRequired > chunkSize)
                {
                    throw new ArgumentException(nameof(chunkSize));
                }

                if (chainBuffers.Count <= level)
                {
                    chainBuffers.Add(new Buffer(chunkSize));
                }

                var spaceAvailable = chainBuffers[level].Capacity - chainBuffers[level].ContentLength;
                if (spaceAvailable < spaceRequired)
                {
                    WriteChainBuffer(level);
                    chainBuffers[level] = new Buffer(chunkSize);
                }

                if (chainBuffers[level].ContentLength == 0)
                {
                    // header to identify chain chunks against known-format content chunks
                    chainBuffers[level].Append((byte)'g');
                    chainBuffers[level].Append((byte)'f');
                    chainBuffers[level].Append((byte)'c');
                    chainBuffers[level].Append((byte)255);

                    // version
                    chainBuffers[level].Append((byte)0);

                    chainBuffers[level].Append((byte)level);
                    chainBuffers[level].Append((byte)(level == 0 ? ChunkType.Content : ChunkType.Chain));
                    chainBuffers[level].Append(new byte[25]);
                }

                chainBuffers[level].Append(address.ToSpan());
            }

            void WriteChainBuffer(int level)
            {
                if (chainBuffers.Count <= level)
                {
                    chainBuffers[level] = new Buffer(chunkSize);
                }

                if (chainBuffers[level].ContentLength == 0)
                {
                    return;
                }

                var chainChunk = _chunkEncryptor.EncryptChunk(InitializationVector.Random(), EncryptionKey.Random(), contentKeyEncryptionKey, hmacKey, chainBuffers[level]);
                if (!repository.TryPut(chainChunk))
                {
                    throw new Exception("Failed to store chunk in repository");
                }

                AppendToChainBuffer(chainChunk.Address, level + 1);
            }

            Address WriteLastChainBuffer()
            {
                var chainChunk = _chunkEncryptor.EncryptChunk(InitializationVector.Random(), EncryptionKey.Random(), contentKeyEncryptionKey, hmacKey, chainBuffers[chainBuffers.Count - 1]);
                if (!repository.TryPut(chainChunk))
                {
                    throw new Exception("Failed to store chunk in repository");
                }
                return chainChunk.Address;
            }

            // Use a different (random) IV and encryption key for each chunk.
            var bufferCount = 0;
            EncryptedChunk? encryptedChunk = null;
            foreach (var buffer in buffers)
            {
                bufferCount++;
                encryptedChunk = _chunkEncryptor.EncryptChunk(InitializationVector.Random(), EncryptionKey.Random(), contentKeyEncryptionKey, hmacKey, buffer);
                if (!repository.TryPut(encryptedChunk))
                {
                    throw new Exception("Failed to store chunk in repository");
                }

                AppendToChainBuffer(encryptedChunk.Address, level: 0);
            }

            if (bufferCount == 1)
            {
                return (encryptedChunk!.Address, ChunkType.Content);
            }

            // write all remaining chain buffers to repository.
            for (int i = 0; i < chainBuffers.Count - 1; i++)
            {
                WriteChainBuffer(i);
            }

            return (WriteLastChainBuffer(), ChunkType.Chain);
        }

        public IEnumerable<Buffer> Decrypt(ChunkType type, KeyEncryptionKey contentKeyEncryptionKey, HmacKey hmacKey, Address address, IChunkRepository repository)
        {
            if (!repository.TryGet(address, out var chunk) || chunk == null)
            {
                throw new Exception($"Address {address} not found in repository");
            }

            var buffer = _chunkEncryptor.DecryptChunk(chunk, contentKeyEncryptionKey, hmacKey);

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
                    throw new Exception();
                }

                for (int i = 32; i < buffer.ContentLength; i += Address.Length)
                {
                    var subchunkAddress = new Address(buffer.ToSpan(i, Address.Length));

                    foreach (var x in Decrypt(subchunkType, contentKeyEncryptionKey, hmacKey, subchunkAddress, repository))
                    {
                        yield return x;
                    }
                }
            }
            else throw new Exception();
        }
    }
}
