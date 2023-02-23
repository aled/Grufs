using System.Diagnostics;
using System.Buffers.Binary;
using Wibblr.Grufs.Encryption;

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

        public ReadOnlySpan<byte> ReadBytes(int count)
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
            return BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(sizeof(ushort)));
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
            return new InitializationVector(ReadBytes(InitializationVector.Length));
        }

        public WrappedEncryptionKey ReadWrappedEncryptionKey()
        {
            return new WrappedEncryptionKey(ReadBytes(WrappedEncryptionKey.Length));
        }

        public Checksum ReadChecksum()
        {
            return new Checksum(ReadBytes(Checksum.Length));
        }
    }
}
