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
        // wrap key

        // unwrap key

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
            var wrappedHmacKey = new WrappedHmacKey(hmacKeyEncryptionKey, hmacKey);

            var plaintext = "The quick brown fox jumps over the lazy dog.\n";

            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            var encryptor = new StreamEncryptor();

            var stream = new MemoryStream(plaintextBytes);

            var repository = new TestChunkRepository();
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
            var wrappedHmacKey = new WrappedHmacKey(hmacKeyEncryptionKey, hmacKey);

            var plaintext = "The quick brown fox jumps over the lazy dog.\n".Repeat(99);
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            var encryptor = new StreamEncryptor();
            
            var stream = new MemoryStream(plaintextBytes);

            var repository = new TestChunkRepository();
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
