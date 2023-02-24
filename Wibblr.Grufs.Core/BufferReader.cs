using System.Diagnostics;
using System.Buffers.Binary;
using Wibblr.Grufs.Encryption;
using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Core
{
    [DebuggerDisplay("{_buffer.ToString()}")]
    public class BufferReader
    {
        public int _offset = 0;
        private readonly ArrayBuffer _buffer;

        public BufferReader(ArrayBuffer buffer)
        {
            _buffer = buffer;
        }

        public int RemainingLength() => _buffer.Length - _offset;

        private void CheckBounds(int i)
        {
            if (_offset + i > _buffer.Length)
            {
                throw new IndexOutOfRangeException();
            }
        }

        public byte ReadByte()
        {
            CheckBounds(1);
            return _buffer.Bytes[_offset++];
        }

        public ReadOnlySpan<byte> ReadSpan()
        {
            int len = ReadInt();
            return ReadKnownLengthSpan(len);
        }

        public ReadOnlySpan<byte> ReadKnownLengthSpan(int count)
        {
            CheckBounds(count);
            var s = _buffer.AsSpan(_offset, count);
            _offset += count;
            return s;
        }

        public string ReadString()
        {
            int charCount = ReadInt();
            int byteCount = charCount * sizeof(char);
            var s = string.Create(charCount, charCount, (chars, state) =>
            {
                for (int i = 0; i < state; i++)
                {
                    chars[i] = (char)ReadUShort();
                }
            });
            return s;
        }

        public ushort ReadUShort()
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(ReadKnownLengthSpan(sizeof(ushort)));
        }

        public int ReadInt()
        {
            return new VarInt(this).Value;
        }

        public long ReadLong()
        {
            return new VarLong(this).Value;
        }

        public Timestamp ReadTimestamp()
        {
            return new Timestamp(this);
        }

        public InitializationVector ReadInitializationVector()
        {
            return new InitializationVector(ReadKnownLengthSpan(InitializationVector.Length));
        }

        public WrappedEncryptionKey ReadWrappedEncryptionKey()
        {
            return new WrappedEncryptionKey(ReadKnownLengthSpan(WrappedEncryptionKey.Length));
        }

        public Checksum ReadChecksum()
        {
            return new Checksum(ReadKnownLengthSpan(Checksum.Length));
        }

        public Address ReadAddress()
        {
            return new Address(ReadKnownLengthSpan(Address.Length));
        }

        public KeyEncryptionKey ReadKeyEncryptionKey()
        {
            return new KeyEncryptionKey(ReadKnownLengthSpan(KeyEncryptionKey.Length));
        }

        public HmacKey ReadHmacKey()
        {
            return new HmacKey(ReadKnownLengthSpan(HmacKey.Length));
        }

        public Salt ReadSalt()
        {
            return new Salt(ReadKnownLengthSpan(Salt.Length));
        }
    }
}
