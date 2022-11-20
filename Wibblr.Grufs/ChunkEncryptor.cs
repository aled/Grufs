using System.Diagnostics;
using System.Security.Cryptography;

namespace Wibblr.Grufs
{
    public class ChunkEncryptor
    {
        public EncryptedChunk EncryptChunk(InitializationVector iv, EncryptionKey key, KeyEncryptionKey keyEncryptionKey, HmacKey hmacKey, Buffer buffer)
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

            //var plaintext = buffer.AsSpan().ToArray();
            //var isAscii = plaintext.All(b => char.IsAscii((char)b));

            //string plaintextPretty;
            //if (isAscii)
            //{
            //    Console.WriteLine($"Encrypted chunk len {encryptedChunk.Content.Length}, {encryptedChunk.Address} with plaintext ascii: " + Encoding.ASCII.GetString(plaintext).Replace("\n", "\\n"));
            //}
            //else
            //{
            //    Console.WriteLine($"Encrypted chunk len {encryptedChunk.Content.Length}, {encryptedChunk.Address} with plaintext (chain)");
            //    WriteChain(plaintext);
            //}

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
