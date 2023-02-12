
namespace Wibblr.Grufs.Core
{
    public interface IChunkSourceFactory
    {
        IChunkSource Create(IByteSource byteSource);
    }
}
