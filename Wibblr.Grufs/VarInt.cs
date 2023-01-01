using System.Numerics;

namespace Wibblr.Grufs
{
    /// <summary>
    /// Uses a variable length encoding that is efficient for small unsigned values.
    /// The total length of the serialized value in bytes is the number of leading binary ones in the first byte.
    /// 0xxxxxxx -> 7-bit int
    /// 10xxxxxx xxxxxxxx -> 14-bit int
    /// 110xxxxx xxxxxxxx xxxxxxxx -> 21-bit int
    /// 1110xxxx xxxxxxxx xxxxxxxx xxxxxxxx -> 28-bit int 
    /// 11110UUU xxxxxxxx xxxxxxxx xxxxxxxx xxxxxxx -> 32-bit int (U = unused bit)
    /// </summary>
    public struct VarInt
    {
        public int Value;

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
                var b = reader.ReadBytes(2);
                Value = ((b0 & 0b00011111) << 16) | (b[0] << 8) | b[1];
            }
            else if (leadingOnes == 3)
            {
                var b = reader.ReadBytes(3);
                Value = ((b0 & 0b00001111) << 24) | (b[0] << 16) | (b[1] << 8) | b[2];
            }
            else if (leadingOnes == 4)
            {
                var b = reader.ReadBytes(4);
                Value = (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
            }
            else if (leadingOnes > 4)
            {
                throw new Exception($"Unable to deserialize varint: invalid initial byte '{b0.ToString("X2")}'");
            }
        }

        public int GetSerializedLength()
        {
            // if 7 bit int, there are at least 25 leading zeros
            // if 14 bit int, there are at least 18 leading zeros
            // etc
            // so 32    -> 1
            //    >= 25 -> 1
            //    >= 18 -> 2
            //    >= 11 -> 3
            //    >= 4  -> 4
            //    else  -> 5
            int leadingZeroCount = BitOperations.LeadingZeroCount(unchecked((uint)Value));

            return 5 - ((leadingZeroCount + 3) / 7) + (leadingZeroCount / 32);
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
                builder.AppendBytes(
                    (byte)(0b10000000 | Value >> 8),
                    (byte)Value
                );
            }
            else if (leadingZeroCount >= 11)
            {
                builder.AppendBytes(
                    (byte)(0b11000000 | Value >> 16),
                    (byte)(Value >> 8),
                    (byte)Value
                );
            }
            else if (leadingZeroCount >= 4)
            {
                builder.AppendBytes(
                    (byte)(0b11100000 | Value >> 24),
                    (byte)(Value >> 16),
                    (byte)(Value >> 8),
                    (byte)Value
                );
            }
            else
            {
                // Use the >>> operator to prevent sign extension for negative values.
                builder.AppendBytes(
                    0b11110000,
                    (byte)(Value >>> 24),
                    (byte)(Value >>> 16),
                    (byte)(Value >>> 8),
                    (byte)Value
                );
            }
        }
    }
}
