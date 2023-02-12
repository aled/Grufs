using System;

namespace Wibblr.Grufs.Core
{
    public class FixedSizeChunkSource : IChunkSource
    {
        private IByteSource _byteSource;
        private int _chunkSize;
        private byte[] _buf;
        private (byte[], long, int)? _next;
        private long _streamOffset;
        private int _bufContentLength;

        public FixedSizeChunkSource(IByteSource byteSource, int chunkSize) 
        { 
            _byteSource = byteSource;
            _chunkSize = chunkSize;
            _buf = new byte[chunkSize];
            TryGetNext();
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
            _next = null;
            while (_byteSource.Available() && _bufContentLength < _chunkSize)
            {
                _buf[_bufContentLength++] = _byteSource.Next();
                _streamOffset++;
            }

            if (_bufContentLength == _chunkSize || (_bufContentLength > 0 && _byteSource.IsCompleted()))
            {
                _next = (_buf, _streamOffset - _bufContentLength, _bufContentLength);
            }
        }
    }
}
