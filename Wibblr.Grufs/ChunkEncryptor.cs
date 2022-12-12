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
        private static readonly int ivOffset = 0;
        private static readonly int wrappedKeyOffset = ivOffset + InitializationVector.Length;
        private static readonly int contentOffset = wrappedKeyOffset + WrappedEncryptionKey.Length;

        public EncryptedChunk EncryptChunk(InitializationVector iv, EncryptionKey key, KeyEncryptionKey keyEncryptionKey, HmacKey addressKey, Buffer buffer)
        {
            var content = EncryptContent(iv, key, keyEncryptionKey, buffer.ToSpan());

            // The address is a hash of the content (excluding checksum) and nothing else
            var hmac = new Hmac(addressKey, buffer.Bytes, 0, buffer.ContentLength);

            return new EncryptedChunk(new Address(hmac.ToSpan()), content);
        }

        public byte[] EncryptContent(InitializationVector iv, EncryptionKey key, KeyEncryptionKey keyEncryptionKey, ReadOnlySpan<byte> source)
        {
            var e = new Encryptor();
            var ciphertextLength = e.CiphertextLength(source.Length);

            // content is:
            //   iv + wrapped-key + encrypt(iv, key, plaintext + checksum) + checksum-of-all-previous-bytes
            //   16 + 40          + len                                    + 32
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

            return content;
        }

        public Buffer DecryptChunkAndVerifyAddress(EncryptedChunk chunk, KeyEncryptionKey keyEncryptionKey, HmacKey addressKey)
        {
            var buffer = DecryptChunk(chunk, keyEncryptionKey);

            // Verify that the chunk is not corrupted using the hmac
            var computedAddress = new Hmac(addressKey, buffer.Bytes, 0, buffer.ContentLength);

            var actual = Vector256.Create(chunk.Address.ToSpan());
            var computed = Vector256.Create(computedAddress.ToSpan());

            if (!Vector256.EqualsAll(actual, computed))
            {
                throw new Exception("Failed to verify chunk - invalid hmac");
            }

            return buffer;
        }


        public Buffer DecryptChunk(EncryptedChunk chunk, KeyEncryptionKey keyEncryptionKey)
        {
            if (chunk.Content.Length < contentOffset + Checksum.Length)
            {
                throw new Exception($"Invalid content length {chunk.Content.Length}");
            }

            var e = new Encryptor();
            var maxPlaintextLength = e.MaxPlaintextLength(chunk.Content.Length - contentOffset - Checksum.Length);

            // Allocate a buffer to hold the decrypted text
            var buffer = new Buffer(maxPlaintextLength);
            var destination = buffer.Bytes.AsSpan();

            var iv = new InitializationVector(new ReadOnlySpan<byte>(chunk.Content, ivOffset, InitializationVector.Length));
            var wrappedKey = new WrappedEncryptionKey(new ReadOnlySpan<byte>(chunk.Content, wrappedKeyOffset, WrappedEncryptionKey.Length));
            var key = wrappedKey.Unwrap(keyEncryptionKey);

            var ciphertextBytes = new ReadOnlySpan<byte>(chunk.Content, contentOffset, chunk.Content.Length - contentOffset - Checksum.Length);

            // Verify checksum before decrypting
            var actualChecksum = new ReadOnlySpan<byte>(chunk.Content, chunk.Content.Length - Checksum.Length, Checksum.Length);
            var computedChecksum = Checksum.Builder.Build(new ReadOnlySpan<byte>(chunk.Content, 0, chunk.Content.Length - Checksum.Length)).ToSpan();

            if (!Vector256.EqualsAll(Vector256.Create(actualChecksum), Vector256.Create(computedChecksum)))
            {
                throw new Exception("Failed to verify chunk - invalid checksum");
            }

            var bytesWritten = e.Decrypt(ciphertextBytes, iv, key, destination);
            buffer.ContentLength = bytesWritten;

            return buffer;
        }
    }
}
