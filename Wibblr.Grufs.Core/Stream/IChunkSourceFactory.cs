
namespace Wibblr.Grufs
{
    public interface IChunkSourceFactory
    {
        IChunkSource Create(IByteSource byteSource);
    }
}
