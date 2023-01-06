using System.Text;

using FluentAssertions;

using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs.Tests
{

    public class StreamStorageTests
    {
        // encrypt stream (single chunk)
        [Fact]
        public void EncryptStreamWithSingleChunk()
        {
            var keyEncryptionKey = new KeyEncryptionKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var hmacKey = new HmacKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var compressor = new Compressor(CompressionAlgorithm.None);

            var plaintext = "The quick brown fox jumps over the lazy dog.\n";

            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            var chunkStorage = new InMemoryChunkStorage();
            var streamStorage = new StreamStorage(keyEncryptionKey, hmacKey, compressor, chunkStorage, 128);

            var stream = new MemoryStream(plaintextBytes);

            var (address, type) = streamStorage.Write(stream);

            chunkStorage.Count().Should().Be(1);

            var decryptedStream = new MemoryStream();
            foreach (var decryptedBuffer in streamStorage.Read(type, address))
            {
                decryptedStream.Write(decryptedBuffer.AsSpan());
            }

            var decryptedText = Encoding.UTF8.GetString(decryptedStream.ToArray());

            decryptedText.Should().Be(plaintext);
        }

        [Fact]
        public void EncryptStreamMultipleLevelsOfTree()
        {
            var keyEncryptionKey = new KeyEncryptionKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var hmacKey = new HmacKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var compressor = new Compressor(CompressionAlgorithm.None);

            var plaintext = "";
            for (int i = 0; i < 999; i++)
            {
                plaintext += $"{i} The quick brown fox jumps over the lazy dog {i}\n";
            }

            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            var chunkStorage = new InMemoryChunkStorage();
            var streamStorage = new StreamStorage(keyEncryptionKey, hmacKey, compressor, chunkStorage, 128);

            var stream = new MemoryStream(plaintextBytes);

            var repository = new InMemoryChunkStorage();
            var (address, type) = streamStorage.Write(stream);
            chunkStorage.Count().Should().BeGreaterThan(1);

            var decryptedStream = new MemoryStream();

            foreach (var decryptedBuffer in streamStorage.Read(type, address))
            {
                decryptedStream.Write(decryptedBuffer.AsSpan());
            }

            var decryptedText = Encoding.UTF8.GetString(decryptedStream.ToArray());

            decryptedText.Should().Be(plaintext);

            Console.WriteLine("Dedup ratio: " + repository.DeduplicationRatio);
        }
    }
}
