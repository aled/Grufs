using System;
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

    public class SftpStorageTests
    {
        private SftpStorage GetSftpStorage(string baseDir)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var text = File.ReadAllText("sftp-credentials.json");

            var sftpCredentials = JsonSerializer.Deserialize<SftpCredentials>(text, options) ?? throw new Exception("Error deserializing SFTP credentials");

            return new SftpStorage(
                sftpCredentials.Hostname ?? throw new Exception("Invalid SFTP hostname"),
                sftpCredentials.Username ?? throw new Exception("Invalid SFTP username"),
                sftpCredentials.Password ?? throw new Exception("Invalid SFTP password"),
                baseDir);
        }

        [Fact]
        public void Upload()
        {
            var storage = GetSftpStorage("grufs");
            var testData = Encoding.ASCII.GetBytes("Hello World!");

            var ok =  storage.Upload("tests/00001/test00001", testData, allowOverwrite: true);
            
            ok.Should().BeTrue();

            var downloaded = storage.Download("tests/00001/test00001");

            Convert.ToHexString(testData).Should().Be(Convert.ToHexString(downloaded));
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

            var encryptor = new StreamEncryptor();
            var stream = new MemoryStream(plaintextBytes);
            var decryptedStream = new MemoryStream();

            using (var repository = GetSftpStorage("grufs"))
            {
                var (address, type) = encryptor.EncryptStream(keyEncryptionKey, wrappedHmacKey, hmacKeyEncryptionKey, stream, repository, 128);

                foreach (var decryptedBuffer in encryptor.Decrypt(type, keyEncryptionKey, hmacKey, address, repository))
                {
                    decryptedStream.Write(decryptedBuffer.ToSpan());
                }
            }

            var decryptedText = Encoding.UTF8.GetString(decryptedStream.ToArray());

            decryptedText.Should().Be(plaintext);
        }
    }
}
