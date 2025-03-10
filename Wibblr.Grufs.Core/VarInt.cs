﻿using System.Numerics;

namespace Wibblr.Grufs.Core
{
    /// <summary>
    /// Uses a variable length encoding that is efficient for small unsigned values.
    /// The number of leading ones in the first byte specifies the number of additional bytes to read.
    /// 0xxxxxxx -> 7-bit int
    /// 10xxxxxx xxxxxxxx -> 14-bit int
    /// 110xxxxx xxxxxxxx xxxxxxxx -> 21-bit int
    /// 1110xxxx xxxxxxxx xxxxxxxx xxxxxxxx -> 28-bit int 
    /// 11110UUU xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxx -> 32-bit int (U = unused bit)
    /// </summary>
    public struct VarInt
    {
        public int Value;

        public static int MaxSerializedLength => 5;

        public VarInt(int i)
        {
            Value = i;
        }

        public static implicit operator int(VarInt v) => v.Value;

        public VarInt(BufferReader reader)
        {
            var b0 = reader.ReadByte();

            var leadingOnes = BitOperations.LeadingZeroCount(unchecked((uint)~(b0 << 24)));

            if (leadingOnes == 0)
            {
                Value = b0;
            }
            else if (leadingOnes == 1)
            {
                Value = ((b0 & 0b00111111) << 8) | reader.ReadByte();
            }
            else if (leadingOnes == 2)
            {
                var b = reader.ReadKnownLengthSpan(2);
                Value = ((b0 & 0b00011111) << 16) | (b[0] << 8) | b[1];
            }
            else if (leadingOnes == 3)
            {
                var b = reader.ReadKnownLengthSpan(3);
                Value = ((b0 & 0b00001111) << 24) | (b[0] << 16) | (b[1] << 8) | b[2];
            }
            else if (leadingOnes == 4)
            {
                var b = reader.ReadKnownLengthSpan(4);
                Value = (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
            }
            else if (leadingOnes > 4)
            {
                throw new Exception($"Unable to deserialize varint: invalid initial byte '{b0.ToString("X2")}'");
            }
        }

        public int GetSerializedLength()
        {
            // in a 7 bit int, there are at least 25 leading zeros
            // in a 14 bit int, there are at least 18 leading zeros
            // etc
            // so
            //       32 leading zeros -> 1 byte varint
            //    >= 25 leading zeros -> 1 byte varint
            //    >= 18 leading zeros -> 2 byte varint
            //    >= 11 leading zeros -> 3 byte varint
            //    >= 4  leading zeros -> 4 byte varint
            //    else                -> 5 byte varint
            int leadingZeroCount = BitOperations.LeadingZeroCount(unchecked((uint)Value));
            
            // This is an optimised version of the expression (5 - ((leadingZeroCount + 3) / 7) + (leadingZeroCount / 32))
            // obtained by substituting in ((x + (x << 3) + 9) >> 6) in place of (x / 7), which works for x <= 69
            // Also change '/ 32' to '>> 5' which makes a big difference.
            // The optimisation reduced the time from 1.36ns to zero, according to benchmarkdotnet.
            return 5 - ((leadingZeroCount + (leadingZeroCount << 3) + 36) >> 6) + (leadingZeroCount >> 5);
        }

        public void SerializeTo(BufferBuilder builder)
        {
            int leadingZeroCount = BitOperations.LeadingZeroCount(unchecked((uint)Value));

            if (leadingZeroCount >= 25)
            {
                builder.AppendByte((byte)Value);
            }
            else if (leadingZeroCount >= 18)
            {
                builder.AppendKnownLengthSpan([
                    (byte)(0b10000000 | Value >> 8),
                    (byte)Value
                ]);
            }
            else if (leadingZeroCount >= 11)
            {
                builder.AppendKnownLengthSpan([
                    (byte)(0b11000000 | Value >> 16),
                    (byte)(Value >> 8),
                    (byte)Value
                ]);
            }
            else if (leadingZeroCount >= 4)
            {
                builder.AppendKnownLengthSpan([
                    (byte)(0b11100000 | Value >> 24),
                    (byte)(Value >> 16),
                    (byte)(Value >> 8),
                    (byte)Value
                ]);
            }
            else
            {
                // Use the >>> operator to prevent sign extension for negative values.
                builder.AppendKnownLengthSpan([
                    0b11110000,
                    (byte)(Value >>> 24),
                    (byte)(Value >>> 16),
                    (byte)(Value >>> 8),
                    (byte)Value
                ]);
            }
        }
    }
}
