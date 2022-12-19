using System.Diagnostics;
using System.Buffers.Binary;

namespace Wibblr.Grufs
{
    [DebuggerDisplay("{_buffer.ToString()}")]
    public class BufferReader
    {
        public int _offset = 0;
        private readonly Buffer _buffer;

        public BufferReader(Buffer buffer)
        {
            _buffer = buffer;
        }

        private void CheckBounds(int i)
        {
            if (_offset + i > _buffer.Length)
            {
                throw new ArgumentOutOfRangeException();
            }
        }

        public byte ReadByte()
        {
            CheckBounds(0);
            return _buffer.Bytes[_offset++];
        }

        public ReadOnlySpan<byte> ReadBytes(int count)
        {
            CheckBounds(count);
            var s = _buffer.AsSpan(_offset, count);
            _offset += count;
            return s;
        }

        public int ReadInt()
        {
            CheckBounds(sizeof(int));
            return BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(sizeof(int)));
        }

        public long ReadLong()
        {
            CheckBounds(sizeof(long));
            return BinaryPrimitives.ReadInt64LittleEndian(ReadBytes(sizeof(long)));
        }
    }
}
