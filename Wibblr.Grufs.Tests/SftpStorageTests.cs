using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using FluentAssertions;

using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs.Tests
{
    internal class SftpCredentials
    {
        public string? Hostname { get; init; }
        public string? Username { get; init; }
        public string? Password { get; init; }
    }

    internal class TemporarySftpStorage : IDisposable
    {
        internal SftpStorage Storage { get; set; }
        internal string BaseDir { get; set; }

        public TemporarySftpStorage()
        {
            BaseDir = $"grufs/test-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var text = File.ReadAllText("sftp-credentials.json");

            var sftpCredentials = JsonSerializer.Deserialize<SftpCredentials>(text, options) ?? throw new Exception("Error deserializing SFTP credentials");

            Storage = (SftpStorage) new SftpStorage(
                    sftpCredentials.Hostname ?? throw new Exception("Invalid SFTP hostname"),
                    sftpCredentials.Username ?? throw new Exception("Invalid SFTP username"),
                    sftpCredentials.Password ?? throw new Exception("Invalid SFTP password"))
                .WithBaseDir(BaseDir);
        } 

        public void Dispose()
        {
            Storage.DeleteDirectory("");
        }
    }

    public class SftpStorageTests
    {
        [Fact]
        public void Upload()
        {
            using (var storage = new TemporarySftpStorage().Storage)
            {
                var testData = Encoding.ASCII.GetBytes("Hello World!");

                var ok = storage.Upload("tests/00001/test00001", testData, OverwriteStrategy.Allow);

                ok.Should().BeTrue();

                storage.TryDownload("tests/00001/test00001", out var downloaded).Should().BeTrue();

                Convert.ToHexString(testData).Should().Be(Convert.ToHexString(downloaded));
            }
        }

        [Fact]
        public void UploadChunk()
        {
            var keyEncryptionKey = new KeyEncryptionKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var hmacKey = new HmacKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var hmacKeyEncryptionKey = new HmacKeyEncryptionKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var wrappedHmacKey = hmacKey.Wrap(hmacKeyEncryptionKey);
            var plaintext = "The quick brown fox jumps over the lazy dog.\n".Repeat(10); // approx 450KB
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            var stream = new MemoryStream(plaintextBytes);
            var decryptedStream = new MemoryStream();

            using (var chunkStorage = new TemporarySftpStorage().Storage)
            {
                var streamStorage = new StreamStorage(chunkStorage);
                var (address, type) = streamStorage.Write(keyEncryptionKey, wrappedHmacKey, hmacKeyEncryptionKey, stream);

                foreach (var decryptedBuffer in streamStorage.Read(type, keyEncryptionKey, hmacKey, address))
                {
                    decryptedStream.Write(decryptedBuffer.ToSpan());
                }
            }

            var decryptedText = Encoding.UTF8.GetString(decryptedStream.ToArray());

            decryptedText.Should().Be(plaintext);
        }

        [Fact]
        public void ListFiles()
        {
            using (var storage = new TemporarySftpStorage().Storage)
            {
                storage.Upload("/a/b/c/d.txt", new byte[] { 0 }, OverwriteStrategy.DenyWithError);
                storage.Upload("a/b/e.txt", new byte[] { 0 }, OverwriteStrategy.DenyWithError);
                storage.Upload("a/b/f.txt", new byte[] { 0 }, OverwriteStrategy.DenyWithError);

                storage.ListFiles("a/").Should().BeEquivalentTo(new[] { "b/e.txt", "b/f.txt", "b/c/d.txt" });
            }
        }

        [Fact]
        public void ListAddresses()
        {
            using (var temp = new TemporarySftpStorage())
            {
                var storage = (IChunkStorage)temp.Storage;

                var address0 = new Address(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                var address1 = new Address(Convert.FromHexString("1000000000000000000000000000000000000000000000000000000000000000"));
                var address2 = new Address(Convert.FromHexString("2000000000000000000000000000000000000000000000000000000000000000"));
                var address3 = new Address(Convert.FromHexString("3000000000000000000000000000000000000000000000000000000000000000"));
                var content = new byte[] { 0 };

                storage.TryPut(new EncryptedChunk(address0, content), OverwriteStrategy.DenyWithError);
                storage.TryPut(new EncryptedChunk(address1, content), OverwriteStrategy.DenyWithError);
                storage.TryPut(new EncryptedChunk(address2, content), OverwriteStrategy.DenyWithError);
                storage.TryPut(new EncryptedChunk(address3, content), OverwriteStrategy.DenyWithError);

                storage.ListAddresses().Should().BeEquivalentTo(new[] { address0, address1, address2, address3 });
            }
        }
    }
}
