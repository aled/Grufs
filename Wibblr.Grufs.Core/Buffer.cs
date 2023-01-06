using System;
using System.Diagnostics;

namespace Wibblr.Grufs
{
    [DebuggerDisplay("{ToString()}")]
    public class Buffer
    {
        internal byte[] Bytes { get; set; }
        internal int Length { get; set; } = 0;

        public Buffer(byte[] buf, int length)
        {
            if (length > buf.Length)
            {
                throw new ArgumentException("Invalid length");
            }

            Bytes = buf;
            Length = length;
        }

        public static Buffer Empty = new Buffer(new byte[0], 0);

        public ReadOnlySpan<byte> AsSpan() => Bytes.AsSpan(0, Length);

        public ReadOnlySpan<byte> AsSpan(int offset, int length)
        {
            if (offset + length > Length)
            {
                throw new IndexOutOfRangeException();
            }
            return Bytes.AsSpan(offset, length);
        }

        public override string ToString()
        {
            return Convert.ToHexString(AsSpan());
        }
    }
}
