using System;
using System.Text;
using System.Numerics;
using System.Diagnostics;

namespace Wibblr.Grufs
{

    [DebuggerDisplay("{ToString()}")]
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

        public Buffer Append(ReadOnlySpan<byte> bytes)
        {
            if (ContentLength + bytes.Length > Capacity)
            {
                throw new Exception("buffer overflow");
            }

            var destination = new Span<byte>(Bytes, ContentLength, bytes.Length);
            bytes.CopyTo(destination);

            ContentLength = ContentLength + bytes.Length;
            
            return this;
        }

        public Buffer Append(byte b)
        {
            if (ContentLength + 1 > Capacity)
            {
                throw new Exception("buffer overflow");
            }

            Bytes[ContentLength] = b;
            ContentLength = ContentLength + 1;

            return this;
        }

        public Buffer Append(int i)
        {
            if (ContentLength + 4 > Capacity)
            {
                throw new Exception("buffer overflow");
            }

            ((IBinaryInteger<int>)i).WriteBigEndian(Bytes, ContentLength);
            ContentLength += 4;

            return this;
        }

        public Buffer Append(long i)
        {
            if (ContentLength + 16 > Capacity)
            {
                throw new Exception("buffer overflow");
            }

            ((IBinaryInteger<long>)i).WriteBigEndian(Bytes, ContentLength);
            ContentLength += 16;
            return this;
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

        public override string ToString()
        {
            if (ToSpan().ToArray().All(x => char.IsAscii((char)x)))
                return Encoding.UTF8.GetString(ToSpan());

            return Convert.ToHexString(ToSpan());
        }
    }
}
