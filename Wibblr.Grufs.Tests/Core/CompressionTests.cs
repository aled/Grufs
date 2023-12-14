using System.Reflection;
using System.Text;

using Wibblr.Grufs.Core;
using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs.Tests
{
    public class CompressionTests_InMemory : CompressionTests<TemporaryInMemoryStorage> { };

    public class CompressionTests_Sqlite : CompressionTests<TemporarySqliteStorage> { };

    public class CompressionTests_Local : CompressionTests<TemporaryLocalStorage> { };

    public class CompressionTests_Sftp : CompressionTests<TemporarySftpStorage> { };

    public abstract class CompressionTests<T> where T : IChunkStorageFactory, new()
    {
        [Theory]
        [InlineData(CompressionAlgorithm.None)]
        [InlineData(CompressionAlgorithm.Deflate)]
        [InlineData(CompressionAlgorithm.Gzip)]
        [InlineData(CompressionAlgorithm.Brotli)]
        [InlineData(CompressionAlgorithm.Zlib)]
        public void EncryptStreamWithCompression(CompressionAlgorithm compressionAlgorithm)
        {
            try
            {
                using (T temporaryStorage = new())
                {
                    var storage = temporaryStorage.GetChunkStorage();
                    storage.Init();

                    var keyEncryptionKey = new KeyEncryptionKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
                    var hmacKey = new HmacKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
                    var compressor = new Compressor(compressionAlgorithm);
                    var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, hmacKey, compressor);
                    var chunkSourceFactory = new ContentDefinedChunkSourceFactory(13);
                    var streamStorage = new StreamStorage(storage, chunkSourceFactory, chunkEncryptor);

                    var plaintext = "";
                    for (int i = 0; i < 999; i++)
                    {
                        plaintext += $"{i} The quick brown fox jumps over the lazy dog {i}\n";
                    }
                    var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                    var stream = new MemoryStream(plaintextBytes);

                    var repository = new InMemoryChunkStorage();
                    var (address, level, stats) = streamStorage.Write(stream);

                    stats.PlaintextLength.Should().Be(plaintextBytes.LongLength);

                    var decryptedStream = new MemoryStream();

                    foreach (var decryptedBuffer in streamStorage.Read(level, address))
                    {
                        decryptedStream.Write(decryptedBuffer.AsSpan());
                    }

                    var decryptedText = Encoding.UTF8.GetString(decryptedStream.ToArray());

                    decryptedText.Should().Be(plaintext);

                    Log.WriteLine(0, "Dedup ratio: " + repository.DeduplicationCompressionRatio());
                }
            }
            catch (TargetInvocationException e) when (e.InnerException is MissingSftpCredentialsException)
            {
                Console.WriteLine("Skipping test due to missing SFTP credentials");
            }
        }
    }
}
