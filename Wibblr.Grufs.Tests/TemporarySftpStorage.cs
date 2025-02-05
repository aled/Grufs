using System.Security.Cryptography;
using System.Text.Json;

using Wibblr.Grufs.Storage;
using Wibblr.Grufs.Storage.Sftp;

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

            _storage = new SftpStorage(sftpCredentials, BaseDir);
        }

        public void Dispose()
        {
            Log.WriteLine(0, $"Deleting temporary directory '{BaseDir}'");

            Task.Run(() => _storage.DeleteDirectory("", CancellationToken.None)).Wait();
        }
    }
}
