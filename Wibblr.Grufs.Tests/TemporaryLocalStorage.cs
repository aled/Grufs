using System.Security.Cryptography;

using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Tests
{
    public class TemporaryLocalStorage : IChunkStorageFactory, IFileStorageFactory, IDisposable
    {
        internal AbstractFileStorage _storage;

        internal string BaseDir { get; set; }

        public IChunkStorage GetChunkStorage() => _storage;
        public AbstractFileStorage GetFileStorage() => _storage;

        public TemporaryLocalStorage()
        {
            BaseDir = Path.Join(Path.GetTempPath(), "grufs", $"test-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}");
            Log.WriteLine(0, $"Using local temporary directory {BaseDir}");

            _storage = new LocalStorage(BaseDir);
            _storage.CreateDirectory("", createParents: true);
        }

        public void Dispose()
        {
            Log.WriteLine(0, $"Deleting temporary directory {BaseDir}");
            _storage.DeleteDirectory("");
        }
    }
}
