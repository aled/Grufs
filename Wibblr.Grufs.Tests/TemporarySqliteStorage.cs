using System.Security.Cryptography;

using Wibblr.Grufs.Storage;
using Wibblr.Grufs.Storage.Sqlite;

namespace Wibblr.Grufs.Tests
{
    public class TemporarySqliteStorage : IChunkStorageFactory
    {
        private string _baseDir;
        private SqliteStorage _storage;

        public TemporarySqliteStorage()
        {
            _baseDir = Path.Join(Path.GetTempPath(), "grufs", $"test-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}");
            _storage = new SqliteStorage(Path.Join(_baseDir, "grufs.sqlite"));
        }

        public IChunkStorage GetChunkStorage()
        {
            Log.WriteLine(0, $"Using local temporary directory {_baseDir}");
            return _storage;
        }

        public void Dispose()
        {
            _storage.Dispose();
            Directory.Delete(_baseDir, true);
        }
    }
}
