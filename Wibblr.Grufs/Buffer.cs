using System;
using System.Text;
using System.Diagnostics;

namespace Wibblr.Grufs
{

    [DebuggerDisplay("{ToString()}")]
    public class Buffer
    {
        internal byte[] Bytes { get; set; }
        public int Length { get; set; } = 0;

        public Buffer(byte[] buf, int length)
        {
            if (length > buf.Length)
            {
                throw new ArgumentException("Invalid length");
            }

            Bytes = buf;
            Length = length;
        }

        public ReadOnlySpan<byte> AsSpan() => Bytes.AsSpan(0, Length);

        public ReadOnlySpan<byte> AsSpan(int offset, int length)
        {
            if (offset + length > Length)
            {
                throw new Exception();
            }
            return Bytes.AsSpan(offset, length);
        }

        public override string ToString()
        {
            if (AsSpan().ToArray().All(x => char.IsAscii((char)x)))
                return Encoding.UTF8.GetString(AsSpan());

            return Convert.ToHexString(AsSpan());
        }
    }
}
