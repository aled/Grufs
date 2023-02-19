using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Tests
{
    public interface IChunkStorageFactory : IDisposable
    {
        IChunkStorage GetChunkStorage();
    }
}
