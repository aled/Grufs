using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

[assembly: InternalsVisibleTo("Wibblr.Grufs.Tests")]

namespace Wibblr.Grufs
{

    /// <summary>
    /// Basic encryption of a buffer using AES
    /// </summary>
    public class Encryptor
    {
        public void WriteChain(byte[] b)
        {
            for (int i = 0; i < b.Length; i += Address.Length)
            {
                Console.WriteLine(Convert.ToHexString(b, i, Address.Length));
            }
            Console.WriteLine();
        }

        private IEnumerable<Buffer> Chunks(Stream stream, int chunkSize)
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

        public (Address, ChunkType) EncryptStream(KeyEncryptionKey contentKeyEncryptionKey, WrappedHmacKey wrappedHmacKey, HmacKeyEncryptionKey hmacKeyEncryptionKey, Stream stream, IChunkRepository repository, int chunkSize = 1024 * 1024)
        {
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
                chainBuffers[level].Append(address.Value);
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

                var chainChunk = EncryptChunk(InitializationVector.Random(), EncryptionKey.Random(), contentKeyEncryptionKey, hmacKey, chainBuffers[level]);
                if (!repository.TryPut(chainChunk))
                {
                    throw new Exception("Failed to store chunk in repository");
                }

                AppendToChainBuffer(chainChunk.Address, level + 1);
            }

            Address WriteLastChainBuffer()
            {
                var chainChunk = EncryptChunk(InitializationVector.Random(), EncryptionKey.Random(), contentKeyEncryptionKey, hmacKey, chainBuffers[chainBuffers.Count - 1]);
                if (!repository.TryPut(chainChunk))
                {
                    throw new Exception("Failed to store chunk in repository");
                }
                return chainChunk.Address;
            }

            // Use a different (random) IV and encryption key for each chunk.
            var bufferCount = 0;
            EncryptedChunk encryptedChunk = null;
            foreach (var buffer in buffers)
            {
                bufferCount++;
                encryptedChunk = EncryptChunk(InitializationVector.Random(), EncryptionKey.Random(), contentKeyEncryptionKey, hmacKey, buffer);
                if (!repository.TryPut(encryptedChunk))
                {
                    throw new Exception("Failed to store chunk in repository");
                }

                AppendToChainBuffer(encryptedChunk.Address, level: 0);
            }

            if (bufferCount == 1)
            {
                return (encryptedChunk.Address, ChunkType.Content);
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
            if (!repository.TryGet(address, out var chunk))
            {
                throw new Exception($"Address {Convert.ToHexString(address.Value)} not found in repository");
            }

            var buffer = DecryptChunk(chunk, contentKeyEncryptionKey, hmacKey);

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
                    var subchunkAddress = new Address(buffer.Bytes, i);

                    foreach (var x in Decrypt(subchunkType, contentKeyEncryptionKey, hmacKey, subchunkAddress, repository))
                    {
                        yield return x;
                    }
                }
            }
            else throw new Exception();
        }

        internal EncryptedChunk EncryptChunk(InitializationVector iv, EncryptionKey key, KeyEncryptionKey keyEncryptionKey, HmacKey hmacKey, Buffer buffer)
        {
            var wrappedKey = new WrappedKey(keyEncryptionKey, key);

            // The HMAC is a hash of the chunk type and the content
            //var hmac = new HMACSHA256(hmacKey.Value).ComputeHash(buffer.Bytes);

            var x = new HMACSHA256(hmacKey.Value);
            var size = x.HashSize;
            x.TransformFinalBlock(buffer.Bytes, 0, buffer.ContentLength);
            var hmac = x.Hash;
            Debug.Assert(hmac.Length == 32);

            var aes = Aes.Create();
            aes.KeySize = EncryptionKey.Length * 8;
            aes.Key = key.Value;
            aes.Padding = PaddingMode.PKCS7;
            var ciphertextLength = aes.GetCiphertextLengthCbc(buffer.ContentLength);

            // content is:
            //   iv + wrapped-key + encrypt(iv, key, plaintext)
            //   16 + 40          + len
            var preambleLength = InitializationVector.Length + WrappedKey.Length;
            var content = new byte[preambleLength + ciphertextLength];
            var destination = new Span<byte>(content, preambleLength, ciphertextLength);

            if (!aes.TryEncryptCbc(buffer.AsSpan(), iv.Value, destination, out _))
            {
                throw new Exception("Failed to encrypt");
            }

            Array.Copy(iv.Value,         0, content, 0, InitializationVector.Length);
            Array.Copy(wrappedKey.Value, 0, content, InitializationVector.Length, WrappedKey.Length);

            var encryptedChunk = new EncryptedChunk
            {
                Address = new Address(hmac),
                Content = content
            };

            var plaintext = buffer.AsSpan().ToArray();
            var isAscii = plaintext.All(b => char.IsAscii((char)b));

            string plaintextPretty;
            if (isAscii)
            {
                Console.WriteLine($"Encrypted chunk len {encryptedChunk.Content.Length}, {encryptedChunk.Address} with plaintext ascii: " + Encoding.ASCII.GetString(plaintext).Replace("\n", "\\n"));
            }
            else
            {
                Console.WriteLine($"Encrypted chunk len {encryptedChunk.Content.Length}, {encryptedChunk.Address} with plaintext (chain)");
                WriteChain(plaintext);
            }

            return encryptedChunk;
        }

        internal Buffer DecryptChunk(EncryptedChunk chunk, KeyEncryptionKey keyEncryptionKey, HmacKey hmacKey)
        {
            Debug.Assert(chunk != null);

            var preambleLength = InitializationVector.Length + WrappedKey.Length;

            if (chunk.Content.Length < preambleLength)
            {
                throw new Exception($"Invalid content length {chunk.Content.Length}");
            }

            var iv = new InitializationVector(chunk.Content, offset: 0);
            var wrappedKey = new WrappedKey(chunk.Content, offset: InitializationVector.Length);
            var key = new EncryptionKey(keyEncryptionKey, wrappedKey); 

            var aes = Aes.Create();
            aes.KeySize = EncryptionKey.Length * 8;
            aes.Key = key.Value;
            aes.Padding = PaddingMode.PKCS7;

            // The plaintext length is unknown, but it is equal or less than the 
            // ciphertext length - 1
            
            var buffer = new Buffer(chunk.Content.Length - preambleLength - 1);
            var destination = buffer.Bytes.AsSpan();
            int bytesWritten;

            if (!aes.TryDecryptCbc(new Span<byte>(chunk.Content, preambleLength, chunk.Content.Length - preambleLength), iv.Value, destination, out bytesWritten))
            {
                throw new Exception("Failed to decrypt");
            }

            buffer.ContentLength = bytesWritten;

            // Verify that the chunk is not corrupted using the hmac
            //var computedHmac = new HMACSHA256(hmacKey.Value).ComputeHash(buffer.Bytes, 0, buffer.ContentLength);
            var x = new HMACSHA256(hmacKey.Value);
            x.TransformFinalBlock(buffer.Bytes, 0, buffer.ContentLength);
            var computedAddress = x.Hash!;

            for (int i = 0; i < Address.Length; i++)
            {
                if (chunk.Address.Value[i] != computedAddress[i])
                {
                    throw new Exception("Failed to verify chunk - invalid hmac");
                }
            }

            return buffer;
        }
    }
}
