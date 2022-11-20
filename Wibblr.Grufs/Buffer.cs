using System.Text;

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

        public void Append(byte[] bytes)
        {
            if (ContentLength + bytes.Length > Capacity)
            {
                throw new Exception("buffer overflow");
            }

            Array.Copy(bytes, 0, Bytes, ContentLength, bytes.Length);
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

        public Span<byte> AsSpan() => Bytes.AsSpan(0, ContentLength);

        public Span<byte> AsSpan(int offset, int length)
        {
            if (offset + length > ContentLength)
            {
                throw new Exception();
            }
            return Bytes.AsSpan(offset, length);
        }
    }
}
