using System;
using System.Diagnostics;
using System.Buffers.Binary;

using Wibblr.Grufs.Encryption;
using Wibblr.Grufs.Storage;
using System.Text;

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

        internal bool CheckBounds(int i)
        {
            if (_offset + i > _buf.Length)
            {
                throw new IndexOutOfRangeException("Cannot append to buffer");
            }
            return true;
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

        public BufferBuilder AppendByte(byte b)
        {
            _buf[_offset++] = b;
            return this;
        }

        public BufferBuilder AppendString(string s)
        {
            var byteCount = Encoding.UTF8.GetByteCount(s);
            AppendInt(byteCount);
            var destination = _buf.AsSpan(_offset, byteCount);
            Encoding.UTF8.GetBytes(s, destination);
            _offset += byteCount;
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
