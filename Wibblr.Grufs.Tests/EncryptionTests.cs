using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using FluentAssertions;
using Wibblr.Grufs;
using Wibblr.Base32;

namespace Wibblr.Grufs.Tests
{
    public static class StringExtensions
    {
        public static string Repeat(this string s, int count)
        {
            return string.Create(s.Length * count, s, (chars, s) =>
            {
                for (int i = 0; i < s.Length * count; i++)
                {
                    chars[i] = s[i % s.Length];
                };
            });
        }
    }

    public class EncryptionTests
    {
        private byte[] Bytes(string hex) => hex.ToCharArray()
                .Chunk(2)
                .Select(x => Convert.ToByte(new string(x), 16))
                .ToArray();

        // wrap key
        // unwrap key
        [Fact]
        public void CheckLengthOfWrappedKeys()
        {
            // Null array of bytes
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            new Action(() => new WrappedEncryptionKey(null, 0)).Should().ThrowExactly<ArgumentNullException>();
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

            // Negative offset
            new Action(() => new WrappedEncryptionKey(new[] {(byte)0}, -1)).Should().ThrowExactly<ArgumentOutOfRangeException>();

            // Invalid array length
            new Action(() => new WrappedEncryptionKey(new[] { (byte)0 }, 0)).Should().ThrowExactly<ArgumentException>();

            // Offset too great
            new Action(() => new WrappedEncryptionKey(Enumerable.Range(0, 40).Select(x => (byte)0).ToArray(), 1))
                .Should().ThrowExactly<ArgumentException>();
        }

        [Fact]
        public void WrapKey()
        {
            // Test data copied from https://www.rfc-editor.org/rfc/rfc3394#section-4
            var kek = new KeyEncryptionKey(Bytes("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F"));
            var key = new EncryptionKey(Bytes("00112233445566778899AABBCCDDEEFF000102030405060708090A0B0C0D0E0F"));
            var expectedWrappedKey = new WrappedEncryptionKey(Bytes("28C9F404C4B810F4CBCCB35CFB87F8263F5786E2D80ED326CBC7F0E71A99F43BFB988B9B7A02DD21"));

            var wrappedKey = key.Wrap(kek);
            wrappedKey.Value.Should().BeEquivalentTo(expectedWrappedKey.Value);

            var unwrappedKey = wrappedKey.Unwrap(kek);
            unwrappedKey.Value.Should().BeEquivalentTo(key.Value);
        }

        // encrypt chunk
        // decrypt chunk
        [Fact]
        public void EncryptChunk()
        {
            var iv = new InitializationVector("00000000000000000000000000".Base32ToBytes(ignorePartialSymbol: true));
            var key = new EncryptionKey("0000000000000000000000000000000000000000000000000000".Base32ToBytes(ignorePartialSymbol: true));
            var keyEncryptionKey = new KeyEncryptionKey("0000000000000000000000000000000000000000000000000000".Base32ToBytes(ignorePartialSymbol: true));
            var hmacKey = new HmacKey("0000000000000000000000000000000000000000000000000000".Base32ToBytes(ignorePartialSymbol: true));

            var plaintext = "The quick brown fox jumps over the lazy dog.";

            var encryptor = new ChunkEncryptor();

            var chunk = encryptor.EncryptChunk(iv, key, keyEncryptionKey, hmacKey, new Buffer(plaintext));

            var decryptedCiphertext = Encoding.ASCII.GetString(encryptor.DecryptChunk(chunk, keyEncryptionKey, hmacKey).AsSpan());

            Console.WriteLine($"plaintext: {plaintext}");
            Console.WriteLine($"address:   {chunk.Address.Value.BytesToBase32()}");
            Console.WriteLine($"content:   {chunk.Content.BytesToBase32()}");

            decryptedCiphertext.Should().Be(plaintext);
        }

        // encrypt chunk from partially filled Buffer
        [Fact]
        public void EncryptChunkFromPartiallyFilledBuffer()
        {
            var iv = new InitializationVector("00000000000000000000000000".Base32ToBytes(ignorePartialSymbol: true));
            var key = new EncryptionKey("0000000000000000000000000000000000000000000000000000".Base32ToBytes(ignorePartialSymbol: true));
            var keyEncryptionKey = new KeyEncryptionKey("0000000000000000000000000000000000000000000000000000".Base32ToBytes(ignorePartialSymbol: true));
            var hmacKey = new HmacKey("0000000000000000000000000000000000000000000000000000".Base32ToBytes(ignorePartialSymbol: true));

            var plaintext = "The quick brown fox jumps over the lazy dog.";
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            var encryptor = new ChunkEncryptor();
            var buffer = new Buffer(100).Write(plaintextBytes);

            var chunk = encryptor.EncryptChunk(iv, key, keyEncryptionKey, hmacKey, buffer);

            var decryptedCiphertext = Encoding.ASCII.GetString(encryptor.DecryptChunk(chunk, keyEncryptionKey, hmacKey).AsSpan());

            Console.WriteLine($"plaintext: {plaintext}");
            Console.WriteLine($"address:   0x{Convert.ToHexString(chunk.Address.Value)}");
            Console.WriteLine($"content:   0x{Convert.ToHexString(chunk.Content)}");

            decryptedCiphertext.Should().Be(plaintext);
        }

        // encrypt stream (single chunk)
        [Fact]
        public void EncryptStreamWithSingleChunk()
        {
            var keyEncryptionKey = new KeyEncryptionKey("0000000000000000000000000000000000000000000000000000".Base32ToBytes(ignorePartialSymbol: true));
            var hmacKey = new HmacKey("0000000000000000000000000000000000000000000000000000".Base32ToBytes(ignorePartialSymbol: true));
            var hmacKeyEncryptionKey = new HmacKeyEncryptionKey("0000000000000000000000000000000000000000000000000000".Base32ToBytes(ignorePartialSymbol: true));
            var wrappedHmacKey = hmacKey.Wrap(hmacKeyEncryptionKey);

            var plaintext = "The quick brown fox jumps over the lazy dog.\n";

            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            var encryptor = new StreamEncryptor();

            var stream = new MemoryStream(plaintextBytes);

            var repository = new InMemoryChunkRepository();
            var (address, type) = encryptor.EncryptStream(keyEncryptionKey, wrappedHmacKey, hmacKeyEncryptionKey, stream, repository, 128);

            repository.Count().Should().Be(1);

            var decryptedStream = new MemoryStream();
            foreach (var decryptedBuffer in encryptor.Decrypt(type, keyEncryptionKey, hmacKey, address, repository))
            {
                decryptedStream.Write(decryptedBuffer.AsSpan());
            }

            var decryptedText = Encoding.UTF8.GetString(decryptedStream.ToArray());

            decryptedText.Should().Be(plaintext);
        }

        // encrypt stream (chain)
        [Fact]
        public void EncryptStreamMultipleLevelsOfChain()
        {
            var keyEncryptionKey = new KeyEncryptionKey("0000000000000000000000000000000000000000000000000000".Base32ToBytes(ignorePartialSymbol: true));           
            var hmacKey = new HmacKey("0000000000000000000000000000000000000000000000000000".Base32ToBytes(ignorePartialSymbol: true));
            var hmacKeyEncryptionKey = new HmacKeyEncryptionKey("0000000000000000000000000000000000000000000000000000".Base32ToBytes(ignorePartialSymbol: true));
            var wrappedHmacKey = hmacKey.Wrap(hmacKeyEncryptionKey);

            var plaintext = "The quick brown fox jumps over the lazy dog.\n".Repeat(99);
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            var encryptor = new StreamEncryptor();
            
            var stream = new MemoryStream(plaintextBytes);

            var repository = new InMemoryChunkRepository();
            var (address, type) = encryptor.EncryptStream(keyEncryptionKey, wrappedHmacKey, hmacKeyEncryptionKey, stream, repository, 128);

            var decryptedStream = new MemoryStream();
            
            foreach (var decryptedBuffer in encryptor.Decrypt(type, keyEncryptionKey, hmacKey, address, repository))
            {
                decryptedStream.Write(decryptedBuffer.AsSpan());
            }

            var decryptedText = Encoding.UTF8.GetString(decryptedStream.ToArray());

            decryptedText.Should().Be(plaintext);

            Console.WriteLine("Dedup ratio: " + repository.DeduplicationRatio);
        }

        // encrypt directory

        // encrypt filename

        // modify directory metadata

        // modify file metadata

        // move file to different directory

        // delete file

        // show history

        // restore deleted file

        // make historic snapshot available
    }
}
