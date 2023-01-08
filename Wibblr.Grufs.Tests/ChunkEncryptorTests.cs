using System;
using System.Text;

using FluentAssertions;

using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs.Tests
{
    public class ChunkEncryptorTests
    {
        // wrap key
        // unwrap key
        [Fact]
        public void CheckLengthOfWrappedKeys()
        {
            // Null array of bytes
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            new Action(() => new WrappedEncryptionKey(null)).Should().ThrowExactly<ArgumentException>();
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

            new Action(() => new WrappedEncryptionKey(new[] { (byte)0 })).Should().ThrowExactly<ArgumentException>();
        }

        [Fact]
        public void WrapKey()
        {
            // Test data copied from https://www.rfc-editor.org/rfc/rfc3394#section-4
            var kek = new KeyEncryptionKey("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F".ToBytes());
            var key = new EncryptionKey("00112233445566778899AABBCCDDEEFF000102030405060708090A0B0C0D0E0F".ToBytes());
            var expectedWrappedKey = new WrappedEncryptionKey("28C9F404C4B810F4CBCCB35CFB87F8263F5786E2D80ED326CBC7F0E71A99F43BFB988B9B7A02DD21".ToBytes());

            var wrappedKey = key.Wrap(kek);
            wrappedKey.ToString().Should().BeEquivalentTo(expectedWrappedKey.ToString());

            var unwrappedKey = wrappedKey.Unwrap(kek);
            unwrappedKey.ToString().Should().BeEquivalentTo(key.ToString());
        }

        // encrypt chunk
        // decrypt chunk
        [Fact]
        public void EncryptChunk()
        {
            var iv = new InitializationVector("00000000000000000000000000000000".ToBytes());
            var key = new EncryptionKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var keyEncryptionKey = new KeyEncryptionKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var hmacKey = new HmacKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var compressor = new Compressor(CompressionAlgorithm.None);

            var plaintext = "The quick brown fox jumps over the lazy dog.";
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            var encryptor = new ChunkEncryptor(keyEncryptionKey, hmacKey, compressor);

            var chunk = encryptor.EncryptContentAddressedChunk(iv, key, plaintextBytes);

            var decryptedCiphertext = Encoding.ASCII.GetString(encryptor.DecryptContentAddressedChunk(chunk).AsSpan());

            Console.WriteLine($"plaintext: {plaintext}");
            Console.WriteLine($"address:   {chunk.Address}");
            Console.WriteLine($"content:   {Convert.ToHexString(chunk.Content)}");

            decryptedCiphertext.Should().Be(plaintext);
        }

        // encrypt chunk from partially filled Buffer
        [Fact]
        public void EncryptChunkFromPartiallyFilledBuffer()
        {
            var iv = new InitializationVector("00000000000000000000000000000000".ToBytes());
            var key = new EncryptionKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var keyEncryptionKey = new KeyEncryptionKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var hmacKey = new HmacKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var compressor = new Compressor(CompressionAlgorithm.None);

            var plaintext = "The quick brown fox jumps over the lazy dog.";
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            var encryptor = new ChunkEncryptor(keyEncryptionKey, hmacKey, compressor);
            var buffer = new BufferBuilder(100).AppendBytes(plaintextBytes).ToBuffer();

            var chunk = encryptor.EncryptContentAddressedChunk(iv, key, buffer.AsSpan());

            var decryptedCiphertext = Encoding.ASCII.GetString(encryptor.DecryptContentAddressedChunk(chunk).AsSpan());

            Console.WriteLine($"plaintext: {plaintext}");
            Console.WriteLine($"address:   0x{chunk.Address}");
            Console.WriteLine($"content:   0x{Convert.ToHexString(chunk.Content)}");

            decryptedCiphertext.Should().Be(plaintext);
        }
    }
}
