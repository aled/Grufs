using System.Numerics;

namespace Wibblr.Grufs
{
    /// <summary>
    /// Takes an input stream, and yields chunks of data that are split based on the properties of the rolling checksum at the split point.
    /// Does not require the stream to be seekable.
    /// </summary>
    public class RollingHashStreamSplitter
    {
        private static readonly int minChunkSize = 512; // must be >= rolling window size
        private static readonly int maxChunkSize = 128 * 1024;

        private Stream _stream;
        private RollingHash _hash;
        private byte[] _buffer;
        private int _bytesRead = 0;
        private int _totalBytesRead = 0;

        public RollingHashStreamSplitter(Stream stream)
        {
            // read from stream until the rolling hash meets the chunking criteria, 
            // then return the chunk
            _stream = stream;
            _buffer = new byte[maxChunkSize];
        }

        /// <summary>
        /// Returns true if the hash value meets some criteria. Adjust the required number of trailing zeros 
        /// in the hash value get the desired average chunk size.
        /// </summary>
        /// <param name="hashValue"></param>
        /// <returns></returns>
        private bool IsSplitCriteria(uint hashValue)
        {
            return BitOperations.TrailingZeroCount(hashValue) == 13;
        }

        public IEnumerable<(byte[], int, int)> Chunks()
        {
            var (buf, length, streamOffset) = Next();
            while (length > 0)
            {
                yield return (buf, length, streamOffset);
                (buf, length, streamOffset) = Next();
            }
        }

        private (byte[], int, int) Next()
        {
            _bytesRead = _stream.ReadAtLeast(_buffer.AsSpan(0, RollingHash.WindowSize), RollingHash.WindowSize, throwOnEndOfStream: false);

            if (_bytesRead <= 0)
            {
                return (_buffer, 0, 0);
            }

            _totalBytesRead += _bytesRead;

            if (_bytesRead < RollingHash.WindowSize)
            {
                return (_buffer, _bytesRead, _totalBytesRead - _bytesRead);
            }

            _hash = new RollingHash(_buffer.AsSpan(0, RollingHash.WindowSize));
            while (_bytesRead < maxChunkSize)
            {
                if (_bytesRead > minChunkSize && IsSplitCriteria(_hash.Value))
                {
                    break;
                }

                int readByte = _stream.ReadByte();

                if (readByte < 0)
                {
                    break;
                }

                _buffer[_bytesRead++] = (byte)readByte;
                _totalBytesRead++;

                _hash.Append(_buffer[_bytesRead - RollingHash.WindowSize - 1], (byte)readByte);
            }

            return (_buffer, _bytesRead, _totalBytesRead - _bytesRead);
        }
    }
}
