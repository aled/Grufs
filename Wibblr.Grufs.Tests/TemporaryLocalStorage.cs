using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Tests
{
    public class TemporaryLocalStorage : IChunkStorageFactory, IDisposable
    {
        internal IChunkStorage _storage;
        internal AutoDeleteDirectory _autoDeleteDirectory = new AutoDeleteDirectory();

        public IChunkStorage GetChunkStorage() => _storage;

        public TemporaryLocalStorage()
        {
            Log.WriteLine(0, $"Using local temporary directory {_autoDeleteDirectory.Path}");
            _storage = new LocalStorage(_autoDeleteDirectory.Path);
        }

        public void Dispose()
        {
            _autoDeleteDirectory.Dispose();
        }
    }
}
