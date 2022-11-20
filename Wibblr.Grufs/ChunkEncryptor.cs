using System.Diagnostics;
using System.Security.Cryptography;

namespace Wibblr.Grufs
{
    public class ChunkEncryptor
    {
        public EncryptedChunk EncryptChunk(InitializationVector iv, EncryptionKey key, KeyEncryptionKey keyEncryptionKey, HmacKey hmacKey, Buffer buffer)
        {
            var wrappedKey = key.Wrap(keyEncryptionKey);

            // The HMAC is a hash of the chunk type and the content
            var hmac = new HMACSHA256(hmacKey.Value).ComputeHash(buffer.Bytes, 0, buffer.ContentLength);

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

            return encryptedChunk;
        }

        public Buffer DecryptChunk(EncryptedChunk chunk, KeyEncryptionKey keyEncryptionKey, HmacKey hmacKey)
        {
            Debug.Assert(chunk != null);

            var preambleLength = InitializationVector.Length + WrappedKey.Length;

            if (chunk.Content.Length < preambleLength)
            {
                throw new Exception($"Invalid content length {chunk.Content.Length}");
            }

            var iv = new InitializationVector(chunk.Content, offset: 0);
            var wrappedKey = new WrappedKey(chunk.Content, offset: InitializationVector.Length);
            var key = wrappedKey.Unwrap(keyEncryptionKey); 

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
            var computedAddress = new HMACSHA256(hmacKey.Value).ComputeHash(buffer.Bytes, 0, buffer.ContentLength);

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
