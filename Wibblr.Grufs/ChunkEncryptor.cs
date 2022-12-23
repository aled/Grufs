using System.Runtime.Intrinsics;

using Renci.SshNet.Messages.Connection;

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

        public EncryptedChunk EncryptChunk(KeyEncryptionKey keyEncryptionKey, HmacKey hmacKey, ReadOnlySpan<byte> plaintext) =>
            EncryptChunk(InitializationVector.Random(), EncryptionKey.Random(), keyEncryptionKey, hmacKey, plaintext);

        public EncryptedChunk EncryptChunk(InitializationVector iv, EncryptionKey key, KeyEncryptionKey keyEncryptionKey, HmacKey addressKey, ReadOnlySpan<byte> plaintext)
        {
            var content = EncryptBytes(iv, key, keyEncryptionKey, plaintext);

            // The address is a hash of the content (excluding checksum) and nothing else
            var hmac = new Hmac(addressKey, plaintext);
            var address = new Address(hmac);

            return new EncryptedChunk(address, content);
        }

        public byte[] EncryptBytes(InitializationVector iv, EncryptionKey key, KeyEncryptionKey keyEncryptionKey, ReadOnlySpan<byte> source)
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

            var checksum = Checksum.Build(content.AsSpan(0, checksumOffset));
            var checksumDestination = new Span<byte>(content, checksumOffset, Checksum.Length);
            checksum.ToSpan().CopyTo(checksumDestination);

            return content;
        }

        public Buffer DecryptChunkAndVerifyAddress(EncryptedChunk chunk, KeyEncryptionKey keyEncryptionKey, HmacKey addressKey)
        {
            // Checksum is validated as part of decryption. 
            var plaintextBuffer = DecryptBytes(chunk.Content, keyEncryptionKey);

            // Additionally verify that the chunk is not corrupted using the hmac
            var computedAddress = new Address(new Hmac(addressKey, plaintextBuffer.AsSpan()));

            if (chunk.Address != computedAddress)
            {
                throw new Exception("Failed to verify chunk - invalid hmac");
            }

            return plaintextBuffer;
        }

        public Buffer DecryptBytes(ReadOnlySpan<byte> bytes, KeyEncryptionKey keyEncryptionKey)
        {
            if (bytes.Length < contentOffset + Checksum.Length)
            {
                throw new Exception($"Invalid content length {bytes.Length}");
            }

            var e = new Encryptor();
            var maxPlaintextLength = e.MaxPlaintextLength(bytes.Length - contentOffset - Checksum.Length);

            // Allocate a buffer to hold the decrypted text
            var buf = new byte[maxPlaintextLength];

            var iv = new InitializationVector(bytes.Slice(ivOffset, InitializationVector.Length));
            var wrappedKey = new WrappedEncryptionKey(bytes.Slice(wrappedKeyOffset, WrappedEncryptionKey.Length));
            var key = wrappedKey.Unwrap(keyEncryptionKey);

            var ciphertextBytes = bytes.Slice(contentOffset, bytes.Length - contentOffset - Checksum.Length);

            // Verify checksum before decrypting
            var actualChecksum = new Checksum(bytes.Slice(bytes.Length - Checksum.Length, Checksum.Length));
            var computedChecksum = Checksum.Build(bytes.Slice(0, bytes.Length - Checksum.Length));

            if (actualChecksum != computedChecksum)
            {
                throw new Exception("Failed to verify chunk - invalid checksum");
            }

            int length = e.Decrypt(ciphertextBytes, iv, key, buf);
            return new Buffer(buf, length);
        }
    }
}