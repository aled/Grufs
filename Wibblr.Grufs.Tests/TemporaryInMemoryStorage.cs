using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Tests
{
    public class TemporaryInMemoryStorage : IChunkStorageFactory
    {
        public IChunkStorage GetChunkStorage()
        {
            return new InMemoryChunkStorage();
        }

        public void Dispose()
        {
            // no op
        }

    }
}
