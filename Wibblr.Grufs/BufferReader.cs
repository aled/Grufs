using System.Diagnostics;
using System.Buffers.Binary;
using System.Text;

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

        public byte PeekByte()
        {
            CheckBounds(0);
            return _buffer.Bytes[_offset];
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

        public ushort ReadUShort()
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(sizeof(ushort)));
        }

        public int ReadInt()
        {
            return BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(sizeof(int)));
        }

        public long ReadLong()
        {
            CheckBounds(sizeof(long));
            return BinaryPrimitives.ReadInt64LittleEndian(ReadBytes(sizeof(long)));
        }

        public PathString ReadPathString()
        {
            return new PathString(this);
        }

        public VarInt ReadVarInt()
        {
            return new VarInt(this);
        }

        public Timestamp ReadTimestamp()
        {
            return new Timestamp(this);
        }
    }
}
