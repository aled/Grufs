using System;

using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs
{
    /// <summary>
    /// 
    /// </summary>
    public class ChunkEncryptor
    {
        private readonly KeyEncryptionKey _keyEncryptionKey;
        private readonly HmacKey _addressKey;
        private readonly Compressor _compressor;

        public ChunkEncryptor(KeyEncryptionKey keyEncryptionKey, HmacKey addressKey, Compressor compressor)
        {
            _keyEncryptionKey = keyEncryptionKey;
            _addressKey = addressKey;
            _compressor = compressor;
        }

        public EncryptedChunk EncryptContentAddressedChunk(ReadOnlySpan<byte> plaintext)
        {
            return EncryptContentAddressedChunk(InitializationVector.Random(), EncryptionKey.Random(), plaintext);
        }

        public EncryptedChunk EncryptContentAddressedChunk(InitializationVector iv, EncryptionKey key, ReadOnlySpan<byte> plaintext)
        {
            var buf = EncryptBytes(iv, key, plaintext);

            // The address is a secure hash of the content (excluding the plaintext chunk checksum) and nothing else
            var hmac = new Hmac(_addressKey, plaintext);
            var address = new Address(hmac.ToSpan());

            return new EncryptedChunk(address, buf);
        }

        public Address GetLookupKeyAddress(ReadOnlySpan<byte> lookupKey)
        {
            var hmac = new Hmac(_addressKey, lookupKey);
            return new Address(hmac.ToSpan());
        }

        public EncryptedChunk EncryptKeyAddressedChunk(ReadOnlySpan<byte> lookupKey, ReadOnlySpan<byte> plaintext)
        {
            return EncryptKeyAddressedChunk(InitializationVector.Random(), EncryptionKey.Random(), lookupKey, plaintext);
        }

        private EncryptedChunk EncryptKeyAddressedChunk(InitializationVector iv, EncryptionKey key, ReadOnlySpan<byte> lookupKey, ReadOnlySpan<byte> plaintext)
        {
            var buf = EncryptBytes(iv, key, plaintext);
            return new EncryptedChunk(GetLookupKeyAddress(lookupKey), buf);
        }

        private byte[] EncryptBytes(InitializationVector iv, EncryptionKey key, ReadOnlySpan<byte> source)
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
                .AppendCiphertext(encryptor, compressedSource, iv, key)
                .AppendChecksum();

            // Return the internal array of the buffer, as we know there is no unused space at the end of the array,
            // and the buffer object goes out of scope here anyway
            return builder.GetUnderlyingArray();
        }

        public Buffer DecryptContentAddressedChunk(EncryptedChunk chunk)
        {
            // Both inner (encrypted) and outer (unencrypted) checksums are validated as part of decryption. 
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

        public Buffer DecryptBytes(byte[] chunkContent)
        {
            var encryptedBuffer = new Buffer(chunkContent, chunkContent.Length);
            var reader = new BufferReader(encryptedBuffer);

            var iv = reader.ReadInitializationVector();
            var wrappedKey = reader.ReadWrappedEncryptionKey();
            var compressionAlgorithm = (CompressionAlgorithm)reader.ReadByte();
            var ciphertextBytes = reader.ReadBytes(reader.RemainingLength() - Checksum.Length);
            var checksum = reader.ReadChecksum();

            // Outer (unencrypted) chunk checksum is validated here, before attempting decryption
            var computedChecksum = Checksum.Build(encryptedBuffer.AsSpan(0, encryptedBuffer.Length - Checksum.Length));
            if (checksum != computedChecksum) 
            {
                throw new Exception("Failed to verify chunk - invalid chunk checksum");
            }

            // Inner (encrypted) checksum is validated as part of decryption
            var key = wrappedKey.Unwrap(_keyEncryptionKey);
            var (buf, length) = new Encryptor().Decrypt(ciphertextBytes, iv, key);

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