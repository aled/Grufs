using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Tests
{
    public interface ITemporaryFileStorage : IDisposable
    {
        AbstractFileStorage GetFileStorage();
    }
}
