using System.Reflection;
using System.Text;

using Wibblr.Grufs.Core;
using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs.Tests.Core
{
    public class StreamStorageTests_InMemory : StreamStorageTests<TemporaryInMemoryStorage> { };
    public class StreamStorageTests_Sqlite : StreamStorageTests<TemporarySqliteStorage> { };
    public class StreamStorageTests_Local : StreamStorageTests<TemporaryLocalStorage> { };

    // SFTP is currently too slow to run this test
    //public class StreamStorageTests_Sftp : StreamStorageTests<TemporarySftpStorage> { };

    public abstract class StreamStorageTests<T> where T : IChunkStorageFactory, new()
    {
        static CancellationToken token = CancellationToken.None;

        [Fact]
        public async Task EncryptZeroLengthStream()
        {
            try
            {
                using (T temporaryStorage = new())
                {
                    var storage = temporaryStorage.GetChunkStorage();
                    await storage.InitAsync(token);

                    var keyEncryptionKey = new KeyEncryptionKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
                    var hmacKey = new HmacKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
                    var compressor = new Compressor(CompressionAlgorithm.None);
                    var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, hmacKey, compressor);
                    var chunkSourceFactory = new FixedSizeChunkSourceFactory(128);
                    var streamStorage = new StreamStorage(storage, chunkSourceFactory, chunkEncryptor);

                    var plaintext = "";
                    var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                    var stream = new MemoryStream(plaintextBytes);

                    var (address, level, stats) = await streamStorage.WriteAsync(stream, token);

                    (await storage.CountAsync(token)).ShouldBe(1);

                    var decryptedStream = new MemoryStream();
                    await foreach (var decryptedBuffer in streamStorage.ReadAsync(level, address, token))
                    {
                        decryptedStream.Write(decryptedBuffer.AsSpan());
                    }

                    var decryptedText = Encoding.UTF8.GetString(decryptedStream.ToArray());

                    decryptedText.ShouldBe(plaintext);
                }
            }
            catch (TargetInvocationException e) when (e.InnerException is MissingSftpCredentialsException)
            { 
                Console.WriteLine("Skipping test due to missing SFTP credentials"); 
            }
        }

        // encrypt stream (single chunk)
        [Fact]
        public async Task EncryptStreamWithSingleChunk()
        {
            try
            {
                using (T temporaryStorage = new())
                {
                    var storage = temporaryStorage.GetChunkStorage(); var keyEncryptionKey = new KeyEncryptionKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
                    await storage.InitAsync(token);

                    var hmacKey = new HmacKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
                    var compressor = new Compressor(CompressionAlgorithm.None);
                    var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, hmacKey, compressor);
                    var chunkSourceFactory = new FixedSizeChunkSourceFactory(128);
                    var streamStorage = new StreamStorage(storage, chunkSourceFactory, chunkEncryptor);

                    var plaintext = "The quick brown fox jumps over the lazy dog.\n";
                    var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                    var stream = new MemoryStream(plaintextBytes);

                    var (address, level, stats) = await streamStorage.WriteAsync(stream, token);

                    (await storage.CountAsync(token)).ShouldBe(1);

                    var decryptedStream = new MemoryStream();
                    await foreach (var decryptedBuffer in streamStorage.ReadAsync(level, address, token))
                    {
                        decryptedStream.Write(decryptedBuffer.AsSpan());
                    }

                    var decryptedText = Encoding.UTF8.GetString(decryptedStream.ToArray());

                    decryptedText.ShouldBe(plaintext);
                }
            }
            catch (TargetInvocationException e) when (e.InnerException is MissingSftpCredentialsException)
            {
                Console.WriteLine("Skipping test due to missing SFTP credentials");
            }
        }

        [Theory]
        [InlineData("fixed", 128)]
        [InlineData("fixed", 256)]
        [InlineData("cdc", 6)]
        [InlineData("cdc", 7)]
        public async Task EncryptStreamMultipleLevelsOfTree(string factoryType, int parameter)
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

            try
            {
                using (T temporaryStorage = new())
                {
                    var storage = temporaryStorage.GetChunkStorage();
                    await storage.InitAsync(token);

                    var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, hmacKey, compressor);
                    var streamStorage = new StreamStorage(storage, chunkSourceFactory, chunkEncryptor);

                    var stream = new MemoryStream(plaintextBytes);
                    var (address, level, stats) = await streamStorage.WriteAsync(stream, token);
                    (await storage.CountAsync(token)).ShouldBeGreaterThan(1);

                    var decryptedStream = new MemoryStream();

                    await foreach (var decryptedBuffer in streamStorage.ReadAsync(level, address, token))
                    {
                        decryptedStream.Write(decryptedBuffer.AsSpan());
                    }

                    var decryptedText = Encoding.UTF8.GetString(decryptedStream.ToArray());

                    decryptedText.ShouldBe(plaintext);

                    //Log.WriteLine(0, "Dedup ratio: " + storage.DeduplicationCompressionRatio());
                    Log.WriteLine(0, $"Stored {await storage.CountAsync(token)} chunks");

                    plaintext = "";
                    for (int i = 10; i < 1099; i++)
                    {
                        plaintext += $"{i} The quick brown fox jumps over the lazy dog {i}\n";
                    }
                    plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                    stream = new MemoryStream(plaintextBytes);
                    (address, level, stats) = await streamStorage.WriteAsync(stream, token);

                    decryptedStream = new MemoryStream();

                    await foreach (var decryptedBuffer in streamStorage.ReadAsync(level, address, token))
                    {
                        decryptedStream.Write(decryptedBuffer.AsSpan());
                    }

                    decryptedText = Encoding.UTF8.GetString(decryptedStream.ToArray());

                    decryptedText.ShouldBe(plaintext);

                    //Log.WriteLine(0, "Dedup ratio: " + storage.DeduplicationCompressionRatio());

                    // Log.WriteLine(0, $"Stored {storage.Count()} chunks");
                }
            }
            catch (TargetInvocationException e) when (e.InnerException is MissingSftpCredentialsException)
            {
                Console.WriteLine("Skipping test due to missing SFTP credentials");
            }
        }
    }
}
