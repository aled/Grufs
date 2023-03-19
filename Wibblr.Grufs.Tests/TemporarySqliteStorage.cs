using Wibblr.Grufs.Storage;
using Wibblr.Grufs.Storage.Sqlite;

namespace Wibblr.Grufs.Tests
{
    public class TemporarySqliteStorage : IChunkStorageFactory
    {
        private SqliteStorage _storage;
        private AutoDeleteDirectory _autoDeleteDirectory;

        public TemporarySqliteStorage()
        {
            _autoDeleteDirectory = new AutoDeleteDirectory();
            _storage = new SqliteStorage(Path.Join(_autoDeleteDirectory.Path, "grufs.sqlite"));
        }

        public IChunkStorage GetChunkStorage()
        {
            Log.WriteLine(0, $"Using local temporary directory {_autoDeleteDirectory.Path}");
            return _storage;
        }

        public void Dispose()
        {
            _storage.Dispose();
            _autoDeleteDirectory.Dispose();
        }
    }
}
