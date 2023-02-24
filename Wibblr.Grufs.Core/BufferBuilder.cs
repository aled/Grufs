using System;
using System.Diagnostics;
using System.Buffers.Binary;

using Wibblr.Grufs.Encryption;
using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Core
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

        public BufferBuilder AppendSpan(ReadOnlySpan<byte> bytes)
        {
            AppendInt(bytes.Length);

            bytes.CopyTo(_buf.AsSpan(_offset, bytes.Length));
            _offset += bytes.Length;
            return this;
        }

        public BufferBuilder AppendKnownLengthSpan(ReadOnlySpan<byte> bytes)
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
            AppendInt(s.Length);

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
            new VarInt(i).SerializeTo(this);
            return this;
        }

        public BufferBuilder AppendLong(long i)
        {
            new VarLong(i).SerializeTo(this);
            return this;
        }

        public BufferBuilder AppendTimestamp(Timestamp t)
        {
            t.SerializeTo(this);
            return this;
        }

        public BufferBuilder AppendInitializationVector(InitializationVector iv)
        {
            AppendKnownLengthSpan(iv.ToSpan());
            return this;
        }

        public BufferBuilder AppendWrappedKey(WrappedEncryptionKey wrappedKey)
        {
            AppendKnownLengthSpan(wrappedKey.ToSpan());
            return this;
        }

        public BufferBuilder AppendKeyEncryptionKey(KeyEncryptionKey kek)
        {
            AppendKnownLengthSpan(kek.ToSpan());
            return this;
        }

        public BufferBuilder AppendHmacKey(HmacKey hmacKey)
        {
            AppendKnownLengthSpan(hmacKey.ToSpan());
            return this;
        }

        public BufferBuilder AppendChecksum()
        {
            var checksum = Checksum.Build(ToSpan());
            AppendKnownLengthSpan(checksum.ToSpan());
            return this;
        }

        public BufferBuilder AppendAddress(Address address)
        {
            AppendKnownLengthSpan(address.ToSpan());
            return this;
        }
        public BufferBuilder AppendSalt(Salt salt)
        {
            AppendKnownLengthSpan(salt.ToSpan());
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

        public int RemainingLength() => _buf.Length - _offset;

        public ArrayBuffer ToBuffer()
        {
            return new ArrayBuffer(_buf, _offset);
        }

        internal byte[] GetUnderlyingArray() => _buf;

        public ReadOnlySpan<byte> ToSpan()
        {
            return new ReadOnlySpan<byte>(_buf, 0, _offset);
        }

        public void Clear()
        {
            _offset = 0;
        }
    }
}
