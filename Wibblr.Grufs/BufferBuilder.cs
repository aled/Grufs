﻿using System;
using System.Diagnostics;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Unicode;
using System.Text;

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

        public Span<byte> GetDestinationSpan(int length)
        {
            CheckBounds(length);
            var destination = _buf.AsSpan(_offset, length);
            _offset += length;
            return destination;
        }

        internal void CheckBounds(int i)
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

        public BufferBuilder AppendUShort(ushort i)
        {
            CheckBounds(sizeof(ushort));
            BinaryPrimitives.WriteUInt16LittleEndian(_buf.AsSpan(_offset, sizeof(ushort)), i);
            _offset += sizeof(ushort);
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

        public BufferBuilder AppendPathString(PathString s)
        {
            s.SerializeTo(this);
            return this;
        }

        public BufferBuilder AppendVarInt(VarInt i)
        {
            i.SerializeTo(this);
            return this;
        }

        public BufferBuilder AppendTimestamp(Timestamp t)
        {
            t.SerializeTo(this);
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
