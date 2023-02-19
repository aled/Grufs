using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Tests
{
    public interface IFileStorageFactory : IDisposable
    {
        AbstractFileStorage GetFileStorage();
    }
}
