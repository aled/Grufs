using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Wibblr.Grufs.Core
{
    /// <summary>
    /// Takes an byte source, and yields chunks of data that are split based on the properties of the rolling checksum at the split point.
    /// Does not require the stream to be seekable.
    /// </summary>
    public class ContentDefinedChunkSource : IChunkSource
    {
        private static readonly int minChunkSize = 512; // must be >= rolling window size
        private static readonly int maxChunkSize = 128 * 1024;

        private IByteSource _byteSource;
        private int _splitOnTrailingZeroCount;
        private RollingHash _hash;
        private byte[] _buffer;
        private long _streamBytesRead = 0;
        private int _bufContentLength = 0;

        private (byte[], long, int)? _next;

        public ContentDefinedChunkSource(IByteSource byteSource, int splitOnTrailingZeroCount)
        {
            // read from stream until the rolling hash meets the chunking criteria, 
            // then return the chunk
            _byteSource = byteSource;
            _splitOnTrailingZeroCount = splitOnTrailingZeroCount;
            _buffer = new byte[maxChunkSize];
            TryGetNext();
        }

        /// <summary>
        /// Returns true if the hash value meets some criteria. Adjust the required number of trailing zeros 
        /// in the hash value get the desired average chunk size.
        /// </summary>
        /// <param name="hashValue"></param>
        /// <returns></returns>
        private bool IsSplitCriteria(uint hashValue)
        {
            return BitOperations.TrailingZeroCount(hashValue) == _splitOnTrailingZeroCount;
        }

        public bool Available()
        {
            if (_next == null)
            {
                TryGetNext();
            }
            return _next != null;
        }

        public bool IsCompleted()
        {
            return !Available() && _byteSource.IsCompleted();
        }

        public (byte[], long, int) Next()
        {
            var ret = _next ?? throw new Exception();
            _bufContentLength = 0;
            _next = null;
            return ret;
        }

        private void TryGetNext()
        {
            while (_byteSource.Available() && _bufContentLength < maxChunkSize)
            {
                _buffer[_bufContentLength++] = _byteSource.Next();
                _streamBytesRead++;

                if (_bufContentLength < RollingHash.WindowSize)
                {
                    continue;
                }
                else if (_bufContentLength == RollingHash.WindowSize)
                {
                    _hash = new RollingHash(_buffer.AsSpan(0, RollingHash.WindowSize));
                }
                else
                {
                    _hash.Roll(_buffer[_bufContentLength - RollingHash.WindowSize - 1], _buffer[_bufContentLength - 1]);
                }

                if (_bufContentLength > minChunkSize && IsSplitCriteria(_hash.Value))
                {
                    _next = (_buffer, _streamBytesRead - _bufContentLength, _bufContentLength);
                    return;
                }
            }

            if (_bufContentLength > 0 && _byteSource.IsCompleted() || _bufContentLength == maxChunkSize)
            {
                _next = (_buffer, _streamBytesRead - _bufContentLength, _bufContentLength);
            }
        }
    }
}
