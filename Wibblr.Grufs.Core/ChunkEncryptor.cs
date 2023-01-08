﻿using System;

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
        private static readonly int compressionAlgorithmOffset = wrappedKeyOffset + WrappedEncryptionKey.Length;
        private static readonly int encryptedContentOffset = compressionAlgorithmOffset + 1;

        private KeyEncryptionKey _keyEncryptionKey;
        private HmacKey _addressKey;
        private Compressor _compressor;

        public ChunkEncryptor(KeyEncryptionKey keyEncryptionKey, HmacKey addressKey, Compressor compressor)
        {
            _keyEncryptionKey = keyEncryptionKey;
            _addressKey = addressKey;
            _compressor = compressor;
        }

        public EncryptedChunk EncryptChunk(ReadOnlySpan<byte> plaintext) =>
            EncryptChunk(InitializationVector.Random(), EncryptionKey.Random(), plaintext);

        public EncryptedChunk EncryptChunk(InitializationVector iv, EncryptionKey key, ReadOnlySpan<byte> plaintext)
        {
            var bytes = EncryptBytes(iv, key, plaintext);

            // The address is a hash of the content (excluding checksum) and nothing else
            var hmac = new Hmac(_addressKey, plaintext);
            var address = new Address(hmac.ToSpan());

            return new EncryptedChunk(address, bytes);
        }

        public Buffer EncryptBytes(InitializationVector iv, EncryptionKey key, ReadOnlySpan<byte> source)
        {
            var encryptor = new Encryptor();

            var wrappedKey = key.Wrap(_keyEncryptionKey);
            var compressedSource = _compressor.Compress(source, out var compressionAlgorithm);
            var ciphertextLength = encryptor.CiphertextLength(compressedSource.Length);

            // content is:
            //   iv + wrapped-key + compression-type + encrypt(compressed-plaintext) + checksum-of-all-previous-bytes
            //   16 + 40          + 1                + ciphertext-length             + 32
            var builder = new BufferBuilder(InitializationVector.Length + WrappedEncryptionKey.Length + 1 + ciphertextLength + Checksum.Length)
                .AppendInitializationVector(iv)
                .AppendWrappedKey(wrappedKey)
                .AppendByte((byte)compressionAlgorithm)
                .AppendCiphertext(encryptor, source, iv, key)
                .AppendChecksum();

            return builder.ToBuffer();
        }

        public Buffer DecryptChunkAndVerifyAddress(EncryptedChunk chunk)
        {
            // Checksum is validated as part of decryption. 
            var plaintextBuffer = DecryptBytes(chunk.Content);

            // Additionally verify/authenticate the chunk with the hmac
            var hmac = new Hmac(_addressKey, plaintextBuffer.AsSpan());
            var computedAddress = new Address(hmac.ToSpan());

            if (chunk.Address != computedAddress)
            {
                throw new Exception("Failed to verify chunk - invalid hmac");
            }

            return plaintextBuffer;
        }

        public Buffer DecryptBytes(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < encryptedContentOffset + Checksum.Length)
            {
                throw new Exception($"Invalid content length {bytes.Length}");
            }

            var e = new Encryptor();
            var maxPlaintextLength = e.MaxPlaintextLength(bytes.Length - encryptedContentOffset - Checksum.Length);

            // Allocate a buffer to hold the decrypted text
            var buf = new byte[maxPlaintextLength];

            var iv = new InitializationVector(bytes.Slice(ivOffset, InitializationVector.Length));
            var wrappedKey = new WrappedEncryptionKey(bytes.Slice(wrappedKeyOffset, WrappedEncryptionKey.Length));
            var key = wrappedKey.Unwrap(_keyEncryptionKey);

            var compressionAlgorithm = (CompressionAlgorithm)bytes[compressionAlgorithmOffset];
            var ciphertextBytes = bytes.Slice(encryptedContentOffset, bytes.Length - encryptedContentOffset - Checksum.Length);

            // Verify checksum before decrypting
            var actualChecksum = new Checksum(bytes.Slice(bytes.Length - Checksum.Length, Checksum.Length));
            var computedChecksum = Checksum.Build(bytes.Slice(0, bytes.Length - Checksum.Length));

            if (actualChecksum != computedChecksum)
            {
                throw new Exception("Failed to verify chunk - invalid checksum");
            }

            int length = e.Decrypt(ciphertextBytes, iv, key, buf);

            switch (compressionAlgorithm)
            {
                case CompressionAlgorithm.None:
                   return new Buffer(buf, length);

                default:
                    var decompressed = new Compressor(compressionAlgorithm).Decompress(buf, length);
                    return new Buffer(decompressed, decompressed.Length);
            };
        }
    }
}