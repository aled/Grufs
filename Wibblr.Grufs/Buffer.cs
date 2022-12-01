using System;
using System.Text;
using System.Numerics;

namespace Wibblr.Grufs
{
    public class Buffer
    {
        public int Capacity { get; set; }
        public byte[] Bytes { get; set; }
        public int ContentLength { get; set; } = 0;

        public Buffer(string s)
        {
            Bytes = Encoding.UTF8.GetBytes(s);
            Capacity = Bytes.Length;
            ContentLength = Bytes.Length;
        }

        public Buffer(int capacity)
        {
            Capacity = capacity;
            Bytes = new byte[capacity];
            ContentLength = 0;
        }

        public byte this[int index]
        {
            get
            {
                if (index >= ContentLength)
                    throw new IndexOutOfRangeException();

                return Bytes[index];
            }
        }

        public Buffer Write(byte[] bytes)
        {
            if (bytes.Length > Capacity)
            {
                throw new Exception("buffer overflow");
            }

            Bytes = bytes;
            ContentLength = bytes.Length;

            return this;
        }

        public void Append(ReadOnlySpan<byte> bytes)
        {
            if (ContentLength + bytes.Length > Capacity)
            {
                throw new Exception("buffer overflow");
            }

            var destination = new Span<byte>(Bytes, ContentLength, bytes.Length);
            bytes.CopyTo(destination);

            ContentLength = ContentLength + bytes.Length;
        }

        public void Append(byte b)
        {
            if (ContentLength + 1 > Capacity)
            {
                throw new Exception("buffer overflow");
            }

            Bytes[ContentLength] = b;
            ContentLength = ContentLength + 1;
        }

        public void Append(UInt128 i)
        {
            if (ContentLength + 16 > Capacity)
            {
                throw new Exception("buffer overflow");
            }

            ((IBinaryInteger<UInt128>)i).WriteBigEndian(Bytes, ContentLength);
            ContentLength += 16;
        }

        public Span<byte> ToSpan() => Bytes.AsSpan(0, ContentLength);

        public Span<byte> ToSpan(int offset, int length)
        {
            if (offset + length > ContentLength)
            {
                throw new Exception();
            }
            return Bytes.AsSpan(offset, length);
        }
    }
}
