using System.Diagnostics;
using System.Runtime.Intrinsics;

using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs
{
    /// <summary>
    /// 
    /// </summary>
    public class ChunkEncryptor
    {
        public EncryptedChunk EncryptChunk(InitializationVector iv, EncryptionKey key, KeyEncryptionKey keyEncryptionKey, HmacKey hmacKey, Buffer buffer)
        {
            var e = new Encryptor();
            var source = buffer.ToSpan();
            var ciphertextLength = e.CiphertextLength(source.Length);

            // content is:
            //   iv + wrapped-key + encrypt(iv, key, plaintext) + checksum-of-all-previous-bytes
            //   16 + 40          + len                         + 32
            var ivOffset = 0;
            var wrappedKeyOffset = ivOffset + InitializationVector.Length;
            var contentOffset = wrappedKeyOffset + WrappedEncryptionKey.Length;
            var checksumOffset = contentOffset + ciphertextLength;

            var content = new byte[checksumOffset + Checksum.Length];
            var destination = new Span<byte>(content, contentOffset, ciphertextLength);

            e.Encrypt(source, iv, key, destination);

            var ivDestination = new Span<byte>(content, ivOffset, InitializationVector.Length);
            iv.ToSpan().CopyTo(ivDestination);

            var wrappedKey = key.Wrap(keyEncryptionKey);
            var wrappedKeyDestination = new Span<byte>(content, wrappedKeyOffset, WrappedEncryptionKey.Length);
            wrappedKey.ToSpan().CopyTo(wrappedKeyDestination);

            var checksum = Checksum.Builder.Build(content.AsSpan(0, checksumOffset));
            var checksumDestination = new Span<byte>(content, checksumOffset, Checksum.Length);
            checksum.ToSpan().CopyTo(checksumDestination);

            // The address is a hash of the content (excluding checksum) and nothing else
            var hmac = new Hmac(hmacKey, buffer.Bytes, 0, buffer.ContentLength);

            return new EncryptedChunk(new Address(hmac.ToSpan()), content);
        }

        public Buffer DecryptChunk(EncryptedChunk chunk, KeyEncryptionKey keyEncryptionKey, HmacKey hmacKey)
        {
            var preambleLength = InitializationVector.Length + WrappedEncryptionKey.Length;
            var postambleLength = Checksum.Length;

            if (chunk.Content.Length < preambleLength + postambleLength)
            {
                throw new Exception($"Invalid content length {chunk.Content.Length}");
            }

            var e = new Encryptor();
            var maxPlaintextLength = e.MaxPlaintextLength(chunk.Content.Length - preambleLength - postambleLength);

            // Allocate a buffer to hold the decrypted text
            var buffer = new Buffer(maxPlaintextLength);
            var destination = buffer.Bytes.AsSpan();
            
            var iv = new InitializationVector(new ReadOnlySpan<byte>(chunk.Content, 0, InitializationVector.Length));
            var wrappedKey = new WrappedEncryptionKey(new ReadOnlySpan<byte>(chunk.Content, InitializationVector.Length, WrappedEncryptionKey.Length));
            var key = wrappedKey.Unwrap(keyEncryptionKey);

            var ciphertextBytes = new ReadOnlySpan<byte>(chunk.Content, preambleLength, chunk.Content.Length - preambleLength - postambleLength);

            // Verify checksum before decrypting
            var actualChecksum = new ReadOnlySpan<byte>(chunk.Content, chunk.Content.Length - postambleLength, postambleLength);
            var computedChecksum = Checksum.Builder.Build(new ReadOnlySpan<byte>(chunk.Content, 0, chunk.Content.Length - postambleLength)).ToSpan();

            if (!Vector256.EqualsAll(Vector256.Create(actualChecksum), Vector256.Create(computedChecksum)))
            {
                throw new Exception("Failed to verify chunk - invalid checksum");
            }

            var bytesWritten = e.Decrypt(ciphertextBytes, iv, key, destination);
            buffer.ContentLength = bytesWritten;

            // Verify that the chunk is not corrupted using the hmac
            var computedAddress = new Hmac(hmacKey, buffer.Bytes, 0, buffer.ContentLength);

            var actual = Vector256.Create(chunk.Address.ToSpan());
            var computed = Vector256.Create(computedAddress.ToSpan());

            if (!Vector256.EqualsAll(actual, computed))
            {
                throw new Exception("Failed to verify chunk - invalid hmac");
            }

            return buffer;
        }
    }
}
