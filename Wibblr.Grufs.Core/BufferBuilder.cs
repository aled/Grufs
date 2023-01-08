using System;
using System.Diagnostics;
using System.Buffers.Binary;
using Wibblr.Grufs.Encryption;

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

        public BufferBuilder AppendBytes(byte b0, byte b1)
        {
            CheckBounds(2);
            _buf[_offset++] = b0; 
            _buf[_offset++] = b1;
            return this;
        }

        public BufferBuilder AppendBytes(byte b0, byte b1, byte b2)
        {
            CheckBounds(3);
            _buf[_offset++] = b0;
            _buf[_offset++] = b1;
            _buf[_offset++] = b2;
            return this;
        }

        public BufferBuilder AppendBytes(byte b0, byte b1, byte b2, byte b3)
        {
            CheckBounds(4);
            _buf[_offset++] = b0;
            _buf[_offset++] = b1;
            _buf[_offset++] = b2;
            _buf[_offset++] = b3;
            return this;
        }

        public BufferBuilder AppendBytes(byte b0, byte b1, byte b2, byte b3, byte b4)
        {
            CheckBounds(5);
            _buf[_offset++] = b0;
            _buf[_offset++] = b1;
            _buf[_offset++] = b2;
            _buf[_offset++] = b3;
            _buf[_offset++] = b4;
            return this;
        }

        public BufferBuilder AppendByte(byte b)
        {
            _buf[_offset++] = b;
            return this;
        }

        public BufferBuilder AppendString(string s)
        {
            AppendVarInt(new VarInt(s.Length));

            // TODO: make this efficient
            foreach (var c in s)
            {
                AppendUShort(c);
            }
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

        public BufferBuilder AppendInitializationVector(InitializationVector iv)
        {
            AppendBytes(iv.ToSpan());
            return this;
        }

        public BufferBuilder AppendWrappedKey(WrappedEncryptionKey wrappedKey)
        {
            AppendBytes(wrappedKey.ToSpan());
            return this;
        }

        public BufferBuilder AppendChecksum()
        {
            var checksum = Checksum.Build(ToSpan());
            AppendBytes(checksum.ToSpan());
            return this;
        }

        public BufferBuilder AppendCiphertext(Encryptor encryptor, ReadOnlySpan<byte> plaintext, InitializationVector iv, EncryptionKey key)
        {
            var ciphertextLength = encryptor.CiphertextLength(plaintext.Length);
            var destination = _buf.AsSpan(_offset, ciphertextLength);
            encryptor.Encrypt(plaintext, iv, key, destination);
            _offset += ciphertextLength;
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
