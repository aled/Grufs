using System.Numerics;

namespace Wibblr.Grufs.Core
{
    /// <summary>
    /// Uses a variable length encoding that is efficient for small unsigned values.
    /// The number of leading ones in the first byte specifies the number of additional bytes to read.
    /// 0xxxxxxx -> 7-bit int
    /// 10xxxxxx xxxxxxxx -> 14-bit int
    /// 110xxxxx xxxxxxxx xxxxxxxx -> 21-bit int
    /// 1110xxxx xxxxxxxx xxxxxxxx xxxxxxxx -> 28-bit int 
    /// 11110xxx xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxx -> 35-bit int
    /// 111110xx xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxx -> 42-bit int
    /// 1111110x xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxx -> 49-bit int
    /// 11111110 xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxxx -> 56-bit int
    /// 11111111 xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxxx -> 64-bit int
    /// </summary>
    public struct VarLong
    {
        public long Value;

        public VarLong(long i)
        {
            Value = i;
        }

        public static implicit operator long(VarLong v) => v.Value;

        public VarLong(BufferReader reader)
        {
            var initialByte = reader.ReadByte();

            var leadingOnes = BitOperations.LeadingZeroCount(unchecked((uint)~(initialByte << 24)));

            if (leadingOnes == 0)
            {
                Value = initialByte;
            }
            else if (leadingOnes == 1)
            {
                Value = ((initialByte & 0b00111111) << 8) | reader.ReadByte();
            }
            else if (leadingOnes == 2)
            {
                var b = reader.ReadKnownLengthSpan(2);
                Value = ((initialByte & 0b00011111) << 16) | (b[0] << 8) | b[1];
            }
            else if (leadingOnes == 3)
            {
                var b = reader.ReadKnownLengthSpan(3);
                Value = ((initialByte & 0b00001111) << 24) | (b[0] << 16) | (b[1] << 8) | b[2];
            }
            else if (leadingOnes == 4)
            {
                var b = reader.ReadKnownLengthSpan(4);
                Value = unchecked((long)(
                    unchecked((ulong)initialByte & 0b00000111) << 32 |
                    unchecked((ulong)b[0]) << 24 |
                    unchecked((ulong)b[1]) << 16 |
                    unchecked((ulong)b[2]) << 8 |
                    b[3]));
             }
            else if (leadingOnes == 5)
            {
                var b = reader.ReadKnownLengthSpan(5);
                Value = unchecked((long)(
                    unchecked((ulong)initialByte & 0b00000011) << 40 |
                    unchecked((ulong)b[0]) << 32 |
                    unchecked((ulong)b[1]) << 24 |
                    unchecked((ulong)b[2]) << 16 |
                    unchecked((ulong)b[3]) << 8 |
                    b[4]));
            }
            else if (leadingOnes == 6)
            {
                var b = reader.ReadKnownLengthSpan(6);
                Value = unchecked((long)(
                    unchecked((ulong)initialByte & 0b00000001) << 48 |
                    unchecked((ulong)b[0]) << 40 |
                    unchecked((ulong)b[1]) << 32 |
                    unchecked((ulong)b[2]) << 24 |
                    unchecked((ulong)b[3]) << 16 |
                    unchecked((ulong)b[4]) << 8 |
                    b[5]));
            }
            else if (leadingOnes == 7)
            {
                var b = reader.ReadKnownLengthSpan(7);
                Value = unchecked((long)(
                    unchecked((ulong)b[0]) << 48 |
                    unchecked((ulong)b[1]) << 40 |
                    unchecked((ulong)b[2]) << 32 |
                    unchecked((ulong)b[3]) << 24 |
                    unchecked((ulong)b[4]) << 16 |
                    unchecked((ulong)b[5]) << 8 |
                    b[6]));
            }
            else // leadingones == 8
            {
                var b = reader.ReadKnownLengthSpan(8);
                Value = unchecked((long)(
                    unchecked((ulong)b[0]) << 56 |
                    unchecked((ulong)b[1]) << 48 |
                    unchecked((ulong)b[2]) << 40 |
                    unchecked((ulong)b[3]) << 32 |
                    unchecked((ulong)b[4]) << 24 |
                    unchecked((ulong)b[5]) << 16 |
                    unchecked((ulong)b[6]) << 8 |
                    b[7]));
            }
        }

        public int GetSerializedLength()
        {
            // in a 7 bit long, there are at least 57 leading zeros
            // in a 14 bit long, there are at least 50 leading zeros
            // etc
            // so
            //       64 leading zeros -> 1 byte varlong
            //    >= 57 leading zeros -> 1 byte varlong
            //    >= 50 leading zeros -> 2 byte varlong
            //    >= 43 leading zeros -> 3 byte varlong
            //    >= 36 leading zeros -> 4 byte varlong
            //    >= 29 leading zeros -> 5 byte varlong
            //    >= 22 leading zeros -> 6 byte varlong
            //    >= 15 leading zeros -> 7 byte varlong
            //    >= 8 leading zeros  -> 8 byte varlong
            //    else                -> 9 byte varlong
            int leadingZeroCount = BitOperations.LeadingZeroCount(unchecked((ulong)Value));

            // This is an optimised version of the expression 9 - ((leadingZeroCount - 1) / 7) + (leadingZeroCount / 64)
            // obtained by substituting in ((x + (x << 3) + 9) >> 6) in place of (x / 7), which works for x <= 69
            // Also change '/ 64' to '>> 6' which makes a big difference.
            // The optimised expression benchmarked to be 200x faster
            return 9 - ((leadingZeroCount - 9 + (leadingZeroCount << 3) + 9) >> 6) + (leadingZeroCount >> 6);
        }

        public void SerializeTo(BufferBuilder builder)
        {
            int leadingZeroCount = BitOperations.LeadingZeroCount(unchecked((ulong)Value));

            if (leadingZeroCount >= 57)
            {
                builder.AppendByte((byte)Value);
            }
            else if (leadingZeroCount >= 50)
            {
                builder.AppendBytes(
                    (byte)(0b10000000 | Value >> 8),
                    (byte)Value
                );
            }
            else if (leadingZeroCount >= 43)
            {
                builder.AppendBytes(
                    (byte)(0b11000000 | Value >> 16),
                    (byte)(Value >> 8),
                    (byte)Value
                );
            }
            else if (leadingZeroCount >= 36)
            {
                builder.AppendBytes(
                    (byte)(0b11100000 | Value >> 24),
                    (byte)(Value >> 16),
                    (byte)(Value >> 8),
                    (byte)Value
                );
            }
            else if (leadingZeroCount >= 29)
            {
                builder.AppendBytes(
                    (byte)(0b11110000 | Value >> 32),
                    (byte)(Value >> 24),
                    (byte)(Value >> 16),
                    (byte)(Value >> 8),
                    (byte)Value
                );
            }
            else if (leadingZeroCount >= 22)
            {
                builder.AppendByte(
                    (byte)(0b11111000 | Value >> 40));
                builder.AppendBytes(
                    (byte)(Value >> 32),
                    (byte)(Value >> 24),
                    (byte)(Value >> 16),
                    (byte)(Value >> 8),
                    (byte)Value
                );
            }
            else if (leadingZeroCount >= 15)
            {
                builder.AppendBytes(
                    (byte)(0b11111100 | Value >> 48),
                    (byte)(Value >> 40));
                builder.AppendBytes(
                    (byte)(Value >> 32),
                    (byte)(Value >> 24),
                    (byte)(Value >> 16),
                    (byte)(Value >> 8),
                    (byte)Value
                );
            }
            else if (leadingZeroCount >= 8)
            {
                builder.AppendBytes(
                    (byte)(0b11111110 | Value >> 56),
                    (byte)(Value >> 48),
                    (byte)(Value >> 40));
                builder.AppendBytes(
                    (byte)(Value >> 32),
                    (byte)(Value >> 24),
                    (byte)(Value >> 16),
                    (byte)(Value >> 8),
                    (byte)Value
                );
            }
            else
            {
                // Use the >>> operator to prevent sign extension for negative values.
                builder.AppendBytes(
                    0b11111111,
                    (byte)(Value >>> 56),
                    (byte)(Value >>> 48),
                    (byte)(Value >>> 40),
                    (byte)(Value >>> 32));
                builder.AppendBytes(
                    (byte)(Value >>> 24),
                    (byte)(Value >>> 16),
                    (byte)(Value >>> 8),
                    (byte)Value
                );
            }
        }
    }
}
