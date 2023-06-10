using System.Security.Cryptography;
using System.Text.Json;

using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Tests
{

    public class TemporarySftpStorage : IChunkStorageFactory, IDisposable
    {
        internal SftpStorage _storage;
        internal string BaseDir { get; set; }

        public IChunkStorage GetChunkStorage() => _storage;

        public TemporarySftpStorage()
        {
            BaseDir = $"grufs/test-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";

            Log.WriteLine(0, $"Using SFTP temporary directory {BaseDir}");

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            string text;
            try
            {
                text = File.ReadAllText("sftp-credentials.json");
            }
            catch(Exception)
            {
                throw new MissingSftpCredentialsException();
            }

            var sftpCredentials = JsonSerializer.Deserialize<SftpCredentials>(text, options) ?? throw new Exception("Error deserializing SFTP credentials");

            _storage = (SftpStorage) new SftpStorage(
                    sftpCredentials.Hostname ?? throw new Exception("Invalid SFTP hostname"),
                    22,
                    sftpCredentials.Username ?? throw new Exception("Invalid SFTP username"),
                    sftpCredentials.Password ?? throw new Exception("Invalid SFTP password"),
                    BaseDir);

            _storage.Init();
        }

        public void Dispose()
        {
            Log.WriteLine(0, $"Deleting temporary directory '{BaseDir}'");

            _storage.DeleteDirectory("");
        }
    }
}
