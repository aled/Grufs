using System.Diagnostics;

using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs
{
    public class ChunkEncryptor
    {
        public EncryptedChunk EncryptChunk(InitializationVector iv, EncryptionKey key, KeyEncryptionKey keyEncryptionKey, HmacKey hmacKey, Buffer buffer)
        {
            var e = new Encryptor();
            var source = buffer.ToSpan();
            var ciphertextLength = e.CiphertextLength(source.Length);

            // content is:
            //   iv + wrapped-key + encrypt(iv, key, plaintext)
            //   16 + 40          + len
            var ivOffset = 0;
            var wrappedKeyOffset = ivOffset + InitializationVector.Length;
            var contentOffset = wrappedKeyOffset + WrappedEncryptionKey.Length;

            var content = new byte[contentOffset + ciphertextLength];
            var destination = new Span<byte>(content, contentOffset, ciphertextLength);

            e.Encrypt(source, iv, key, destination);

            var ivDestination = new Span<byte>(content, ivOffset, InitializationVector.Length);
            iv.ToSpan().CopyTo(ivDestination);

            var wrappedKey = key.Wrap(keyEncryptionKey);
            var wrappedKeyDestination = new Span<byte>(content, wrappedKeyOffset, WrappedEncryptionKey.Length);
            wrappedKey.ToSpan().CopyTo(wrappedKeyDestination);

            // The address is a hash of the content and nothing else
            var address = new Address(hmacKey, buffer.Bytes, 0, buffer.ContentLength);

            return new EncryptedChunk(address, content);
        }

        public Buffer DecryptChunk(EncryptedChunk chunk, KeyEncryptionKey keyEncryptionKey, HmacKey hmacKey)
        {
            Debug.Assert(chunk != null);

            var preambleLength = InitializationVector.Length + WrappedEncryptionKey.Length;

            if (chunk.Content.Length < preambleLength)
            {
                throw new Exception($"Invalid content length {chunk.Content.Length}");
            }

            var e = new Encryptor();
            var maxPlaintextLength = e.MaxPlaintextLength(chunk.Content.Length - preambleLength);

            // Allocate a buffer to hold the decrypted text
            var buffer = new Buffer(maxPlaintextLength);
            var destination = buffer.Bytes.AsSpan();
            
            var ivBytes = new ReadOnlySpan<byte>(chunk.Content, 0, InitializationVector.Length);
            var iv = new InitializationVector(ivBytes);

            var wrappedKeyBytes = new ReadOnlySpan<byte>(chunk.Content, InitializationVector.Length, WrappedEncryptionKey.Length);
            var wrappedKey = new WrappedEncryptionKey(wrappedKeyBytes);
            var key = wrappedKey.Unwrap(keyEncryptionKey);

            var ciphertextBytes = new ReadOnlySpan<byte>(chunk.Content, preambleLength, chunk.Content.Length - preambleLength);

            var bytesWritten = e.Decrypt(ciphertextBytes, iv, key, destination);           
            buffer.ContentLength = bytesWritten;

            // Verify that the chunk is not corrupted using the hmac
            var computedAddress = new Address(hmacKey, buffer.Bytes, 0, buffer.ContentLength);

            if (chunk.Address != computedAddress)
            {
                throw new Exception("Failed to verify chunk - invalid hmac");
            }

            return buffer;
        }
    }
}
