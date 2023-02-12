using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using FluentAssertions;

using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs.Tests
{
    public enum TemporaryStorageFactoryType
    {
        Directory,
        Sftp
    }

    internal class TemporaryStorageFactory
    {
        public ITemporaryStorage GetTemporaryStorage(TemporaryStorageFactoryType type)
        {
            return type switch
            {
                TemporaryStorageFactoryType.Directory => new TemporaryDirectoryStorage(),
                TemporaryStorageFactoryType.Sftp => new TemporarySftpStorage(),
                _ => throw new Exception()
            };
        }
    }

    internal interface ITemporaryStorage : IDisposable
    {
        AbstractFileStorage GetStorage();
    }

    internal class TemporaryDirectoryStorage : ITemporaryStorage, IDisposable
    {
        internal AbstractFileStorage _storage;
        internal string BaseDir { get; set; }

        public AbstractFileStorage GetStorage() => _storage;

        public TemporaryDirectoryStorage() 
        {
            BaseDir = Path.Join(Path.GetTempPath(), "grufs", $"test-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}");
            Console.WriteLine($"Using local temporary directory {BaseDir}");

            _storage = new DirectoryStorage(BaseDir);
            _storage.CreateDirectory("", createParents: true);
        }

        public void Dispose()
        {
            Console.WriteLine($"Deleting temporary directory {BaseDir}");
            _storage.DeleteDirectory("");
        }
    }

    internal class TemporarySftpStorage : ITemporaryStorage, IDisposable
    {
        internal AbstractFileStorage _storage;
        internal string BaseDir { get; set; }

        public AbstractFileStorage GetStorage() => _storage;

        public TemporarySftpStorage()
        {
            BaseDir = $"grufs/test-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";

            Console.WriteLine($"Using SFTP temporary directory {BaseDir}");

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var text = File.ReadAllText("sftp-credentials.json");

            var sftpCredentials = JsonSerializer.Deserialize<SftpCredentials>(text, options) ?? throw new Exception("Error deserializing SFTP credentials");

            _storage = (SftpStorage) new SftpStorage(
                    sftpCredentials.Hostname ?? throw new Exception("Invalid SFTP hostname"),
                    22,
                    sftpCredentials.Username ?? throw new Exception("Invalid SFTP username"),
                    sftpCredentials.Password ?? throw new Exception("Invalid SFTP password"),
                    BaseDir);

            _storage.CreateDirectory("", createParents: true);
        }

        public void Dispose()
        {
            Console.WriteLine($"Deleting temporary directory {BaseDir}");

            _storage.DeleteDirectory("");
        }
    }

    public class FileStorageTests
    {
        [Theory]
        [InlineData(TemporaryStorageFactoryType.Directory)]
        [InlineData(TemporaryStorageFactoryType.Sftp)]
        public void Upload(TemporaryStorageFactoryType type)
        {
            using (ITemporaryStorage temporaryStorage = new TemporaryStorageFactory().GetTemporaryStorage(type))
            {
                var storage = temporaryStorage.GetStorage();

                var testData = Encoding.ASCII.GetBytes("Hello World!");

                storage.WriteFile("tests/00001/test00001", testData, OverwriteStrategy.Allow, createDirectories: true).Should().Be(WriteFileStatus.Success);
                storage.ReadFile("tests/00001/test00001", out var downloaded).Should().Be(ReadFileStatus.Success);

                Convert.ToHexString(testData).Should().Be(Convert.ToHexString(downloaded));
            }
        }

        [Theory]
        [InlineData(TemporaryStorageFactoryType.Directory)]
        [InlineData(TemporaryStorageFactoryType.Sftp)]
        public void UploadChunk(TemporaryStorageFactoryType type)
        {
            var keyEncryptionKey = new KeyEncryptionKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var hmacKey = new HmacKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var compressor = new Compressor(CompressionAlgorithm.None);
            var plaintext = "The quick brown fox jumps over the lazy dog.\n".Repeat(10); // approx 450KB
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            var stream = new MemoryStream(plaintextBytes);
            var decryptedStream = new MemoryStream();

            using (ITemporaryStorage temporaryStorage = new TemporaryStorageFactory().GetTemporaryStorage(type))
            {
                var storage = temporaryStorage.GetStorage();
                var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, hmacKey, compressor);
                var chunkSourceFactory = new FixedSizeChunkSourceFactory(128);
                var streamStorage = new StreamStorage(storage, chunkSourceFactory, chunkEncryptor);
                var (address, level, stats) = streamStorage.Write(stream);

                foreach (var decryptedBuffer in streamStorage.Read(level, address))
                {
                    decryptedStream.Write(decryptedBuffer.AsSpan());
                }
            }

            var decryptedText = Encoding.UTF8.GetString(decryptedStream.ToArray());

            decryptedText.Should().Be(plaintext);
        }

        [Theory]
        [InlineData(TemporaryStorageFactoryType.Directory)]
        [InlineData(TemporaryStorageFactoryType.Sftp)]
        public void ListFiles(TemporaryStorageFactoryType type)
        {
            using (ITemporaryStorage temporaryStorage = new TemporaryStorageFactory().GetTemporaryStorage(type))
            {
                var storage = temporaryStorage.GetStorage();

                storage.CreateDirectory("a/b/c", createParents: true);

                storage.WriteFile("/a/b/c/d.txt", new byte[] { 0 }, OverwriteStrategy.Deny).Should().Be(WriteFileStatus.Success);
                storage.WriteFile("a/b/e.txt", new byte[] { 0 }, OverwriteStrategy.Deny).Should().Be(WriteFileStatus.Success); ;
                storage.WriteFile("a/b/f.txt", new byte[] { 0 }, OverwriteStrategy.Deny).Should().Be(WriteFileStatus.Success); ;

                var list = storage.ListFiles("a/", recursive: true)
                    .Select(x => new StoragePath(x.Parts, '/'))
                    .Select(x => x.ToString())
                    .Should().BeEquivalentTo(new[] { "b/e.txt", "b/f.txt", "b/c/d.txt" });
            }
        }

        [Theory]
        [InlineData(TemporaryStorageFactoryType.Directory)]
        [InlineData(TemporaryStorageFactoryType.Sftp)]
        public void ListAddresses(TemporaryStorageFactoryType type)
        {
            using (ITemporaryStorage temporaryStorage = new TemporaryStorageFactory().GetTemporaryStorage(type))
            {
                var storage = (IChunkStorage)temporaryStorage.GetStorage();

                var address0 = new Address(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                var address1 = new Address(Convert.FromHexString("1000000000000000000000000000000000000000000000000000000000000000"));
                var address2 = new Address(Convert.FromHexString("2000000000000000000000000000000000000000000000000000000000000000"));
                var address3 = new Address(Convert.FromHexString("3000000000000000000000000000000000000000000000000000000000000000"));
                var content = new byte[] { 0 };

                storage.Put(new EncryptedChunk(address0, content), OverwriteStrategy.Deny);
                storage.Put(new EncryptedChunk(address1, content), OverwriteStrategy.Deny);
                storage.Put(new EncryptedChunk(address2, content), OverwriteStrategy.Deny);
                storage.Put(new EncryptedChunk(address3, content), OverwriteStrategy.Deny);

                storage.ListAddresses().Should().BeEquivalentTo(new[] { address0, address1, address2, address3 });
            }
        }
    }
}
