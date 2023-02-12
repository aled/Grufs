
namespace Wibblr.Grufs.Core
{
    public class ContentDefinedChunkSourceFactory : IChunkSourceFactory
    {
        private int _splitOnTrailingZeroCount;

        public ContentDefinedChunkSourceFactory(int splitOnTrailingZeroCount)
        {
            if (splitOnTrailingZeroCount < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(splitOnTrailingZeroCount));
            }

            _splitOnTrailingZeroCount = splitOnTrailingZeroCount;
        }

        public IChunkSource Create(IByteSource byteSource)
        {
            return new ContentDefinedChunkSource(byteSource, _splitOnTrailingZeroCount);
        }
    }
}
