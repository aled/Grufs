using System.Security.Cryptography;
using System.Text.Json;

using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Tests
{
    internal class TemporarySftpStorage : ITemporaryFileStorage, IDisposable
    {
        internal AbstractFileStorage _storage;
        internal string BaseDir { get; set; }

        public AbstractFileStorage GetFileStorage() => _storage;

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
}
