using System.Text;

using FluentAssertions;

using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs.Tests
{
    public class StreamStorageTests
    {
        [Fact]
        public void EncryptZeroLengthStream()
        {
            var chunkStorage = new InMemoryChunkStorage();
            var keyEncryptionKey = new KeyEncryptionKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var hmacKey = new HmacKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var compressor = new Compressor(CompressionAlgorithm.None);
            var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, hmacKey, compressor);
            var chunkSourceFactory = new FixedSizeChunkSourceFactory(128);
            var streamStorage = new StreamStorage(chunkStorage, chunkSourceFactory, chunkEncryptor);

            var plaintext = "";
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var stream = new MemoryStream(plaintextBytes);

            var (address, level, stats) = streamStorage.Write(stream);

            chunkStorage.Count().Should().Be(1);

            var decryptedStream = new MemoryStream();
            foreach (var decryptedBuffer in streamStorage.Read(level, address))
            {
                decryptedStream.Write(decryptedBuffer.AsSpan());
            }

            var decryptedText = Encoding.UTF8.GetString(decryptedStream.ToArray());

            decryptedText.Should().Be(plaintext);
        }

        // encrypt stream (single chunk)
        [Fact]
        public void EncryptStreamWithSingleChunk()
        {
            var chunkStorage = new InMemoryChunkStorage();
            var keyEncryptionKey = new KeyEncryptionKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var hmacKey = new HmacKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var compressor = new Compressor(CompressionAlgorithm.None);
            var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, hmacKey, compressor);
            var chunkSourceFactory = new FixedSizeChunkSourceFactory(128);
            var streamStorage = new StreamStorage(chunkStorage, chunkSourceFactory, chunkEncryptor);

            var plaintext = "The quick brown fox jumps over the lazy dog.\n";
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var stream = new MemoryStream(plaintextBytes);

            var (address, level, stats) = streamStorage.Write(stream);

            chunkStorage.Count().Should().Be(1);

            var decryptedStream = new MemoryStream();
            foreach (var decryptedBuffer in streamStorage.Read(level, address))
            {
                decryptedStream.Write(decryptedBuffer.AsSpan());
            }

            var decryptedText = Encoding.UTF8.GetString(decryptedStream.ToArray());

            decryptedText.Should().Be(plaintext);
        }

        [Theory]
        [InlineData("fixed", 128)]
        [InlineData("fixed", 256)]
        [InlineData("cdc", 6)]
        [InlineData("cdc", 7)]
        public void EncryptStreamMultipleLevelsOfTree(string factoryType, int parameter)
        {
            IChunkSourceFactory chunkSourceFactory = factoryType == "fixed"
                ? new FixedSizeChunkSourceFactory(parameter)
                : new ContentDefinedChunkSourceFactory(parameter);

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
            var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, hmacKey, compressor);
            var streamStorage = new StreamStorage(chunkStorage, chunkSourceFactory, chunkEncryptor);

            var stream = new MemoryStream(plaintextBytes);
            var (address, level, stats) = streamStorage.Write(stream);
            chunkStorage.Count().Should().BeGreaterThan(1);

            var decryptedStream = new MemoryStream();

            foreach (var decryptedBuffer in streamStorage.Read(level, address))
            {
                decryptedStream.Write(decryptedBuffer.AsSpan());
            }

            var decryptedText = Encoding.UTF8.GetString(decryptedStream.ToArray());

            decryptedText.Should().Be(plaintext);

            Console.WriteLine("Dedup ratio: " + chunkStorage.DeduplicationCompressionRatio());
            Console.WriteLine($"Stored {chunkStorage.Count()} chunks");

            plaintext = "";
            for (int i = 10; i < 1099; i++)
            {
                plaintext += $"{i} The quick brown fox jumps over the lazy dog {i}\n";
            }
            plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            stream = new MemoryStream(plaintextBytes);
            (address, level, stats) = streamStorage.Write(stream);

            decryptedStream = new MemoryStream();

            foreach (var decryptedBuffer in streamStorage.Read(level, address))
            {
                decryptedStream.Write(decryptedBuffer.AsSpan());
            }

            decryptedText = Encoding.UTF8.GetString(decryptedStream.ToArray());

            decryptedText.Should().Be(plaintext);

            Console.WriteLine("Dedup ratio: " + chunkStorage.DeduplicationCompressionRatio());

            Console.WriteLine($"Stored {chunkStorage.Count()} chunks");
        }
    }
}
