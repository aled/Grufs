using System;

namespace Wibblr.Grufs
{
    public class StreamByteSource : IByteSource
    {
        // For a stream, there is no distinction between available and completed;
        // if there are bytes available then it is not completed and vice versa
        private Stream _stream;
        private byte? _next;

        public StreamByteSource(Stream stream)
        {
            _stream = stream;
            TryReadNext();
        }

        public bool Available()
        {
            if (_next == null)
            {
                TryReadNext();
            }
            return _next != null;
        }

        public bool IsCompleted()
        {
            return _next == null;
        }

        public byte Next()
        {
            var ret = _next ?? throw new Exception();
            TryReadNext();
            return ret;
        }

        private void TryReadNext()
        {
            int read = _stream.ReadByte();
            _next = read < 0 ? null : (byte)read;
        }
    }
}
