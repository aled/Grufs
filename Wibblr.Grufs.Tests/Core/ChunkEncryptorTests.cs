using System.Text;

using Wibblr.Grufs.Core;
using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs.Tests
{
    public class ChunkEncryptorTests
    {
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
