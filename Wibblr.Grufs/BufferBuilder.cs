using System;
using System.Diagnostics;
using System.Buffers.Binary;

namespace Wibblr.Grufs
{
    [DebuggerDisplay("{_buffer.ToString()}")]
    public class BufferBuilder
    {
        private readonly byte[] _buf;
        private int _offset;

        public BufferBuilder(int capacity)
        {
            _buf = new byte[capacity];
        }

        private void CheckBounds(int i)
        {
            if (_offset + i > _buf.Length)
            {
                throw new IndexOutOfRangeException("Cannot append to buffer");
            }
        }

        public BufferBuilder AppendBytes(ReadOnlySpan<byte> bytes)
        {
            bytes.CopyTo(_buf.AsSpan(_offset, bytes.Length));
            _offset += bytes.Length;
            return this;
        }

        public BufferBuilder AppendByte(byte b)
        {
            _buf[_offset] = b;
            _offset += 1;
            return this;
        }

        public BufferBuilder AppendInt(int i)
        {
            CheckBounds(sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(_buf.AsSpan(_offset, sizeof(int)), i);
            _offset += sizeof(int);
            return this;    
        }

        public BufferBuilder AppendLong(long i)
        {
            CheckBounds(sizeof(long));
            BinaryPrimitives.WriteInt64LittleEndian(_buf.AsSpan(_offset, sizeof(long)), i);
            _offset += sizeof(long);
            return this;
        }

        public Buffer ToBuffer()
        {
            return new Buffer(_buf, _offset);
        }

        public ReadOnlySpan<byte> ToSpan()
        {
            return new ReadOnlySpan<byte>(_buf, 0, _offset);
        }
    }
}
