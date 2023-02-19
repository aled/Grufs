using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Tests
{
    internal interface ITemporaryFileStorage : IDisposable
    {
        AbstractFileStorage GetFileStorage();
    }
}
