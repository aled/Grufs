
namespace Wibblr.Grufs.Core
{
    public class FixedSizeChunkSourceFactory : IChunkSourceFactory
    {
        private int _chunkSize;

        public FixedSizeChunkSourceFactory(int chunkSize)
        {
            if (chunkSize < 128)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize));
            }

            _chunkSize = chunkSize;
        }

        public IChunkSource Create(IByteSource byteSource)
        {
            return new FixedSizeChunkSource(byteSource, _chunkSize);
        }
    }
}
