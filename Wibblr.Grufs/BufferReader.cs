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

        public string ReadString()
        {
            int charCount = ReadVarInt();
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
            return BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(sizeof(int)));
        }

        public long ReadLong()
        {
            return BinaryPrimitives.ReadInt64LittleEndian(ReadBytes(sizeof(long)));
        }

        public VarInt ReadVarInt()
        {
            return new VarInt(this);
        }

        public Timestamp ReadTimestamp()
        {
            return new Timestamp(this);
        }

        public RepositoryFilename ReadRepositoryFilename()
        {
            return new RepositoryFilename(this);
        }

        public RepositoryDirectoryPath ReadRepositoryDirectoryPath()
        {
            return new RepositoryDirectoryPath(this);
        }

        public RepositoryFile ReadRepositoryFile()
        {
            return new RepositoryFile(this);
        }

        public RepositoryDirectory ReadRepositoryDirectory()
        {
            return new RepositoryDirectory(this);
        }

    }
}
