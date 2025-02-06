using Wibblr.Grufs.Storage;
using Wibblr.Grufs.Storage.Server;

namespace Wibblr.Grufs.Tests
{
    public class TemporaryServerStorage : IChunkStorageFactory, IDisposable
    {
        internal IChunkStorage _storage;
        internal string _uniquifier;

        public IChunkStorage GetChunkStorage() => _storage;

        public TemporaryServerStorage()
        {
            _uniquifier = Utils.GetUniquifier();
            Log.WriteLine(0, $"Using temporary server location {_uniquifier}");
            _storage = new ServerStorage("localhost", 8080, _uniquifier);
        }

        public void Dispose()
        {
            // delete unique location
        }
    }
}
