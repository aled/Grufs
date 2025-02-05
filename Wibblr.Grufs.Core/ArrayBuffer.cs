using System;
using System.Diagnostics;

namespace Wibblr.Grufs.Core
{
    [DebuggerDisplay("{ToString()}")]
    public class ArrayBuffer
    {
        internal byte[] Bytes { get; set; }
        internal int Length { get; set; } = 0;

        public ArrayBuffer(byte[] buf, int length)
        {
            if (length > buf.Length)
            {
                throw new ArgumentException("Invalid length");
            }

            Bytes = buf;
            Length = length;
        }

        public ArrayBuffer(byte[] buf)
        {
            Bytes = buf;
            Length = buf.Length;
        }

        public static ArrayBuffer Empty = new ArrayBuffer(new byte[0], 0);

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
