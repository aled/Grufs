using System;
using System.Diagnostics;
using Wibblr.Base32;

namespace Wibblr.Grufs
{
    [DebuggerDisplay("{ToString()}")]
    public class Address
    {
        public static int Length = 32;

        public byte[] Value { get; init; }

        public Address(byte[] value)
        {
            if (value.Length != Length)
            {
                throw new Exception($"Invalid address length (expected {Length}; actual {value.Length}");
            }

            Value = value;
        }

        public Address(byte[] buffer, int offset)
        {
            if (buffer.Length - offset < Length)
            {
                throw new Exception($"Invalid address length/offset (expected {Length}; actual {buffer.Length}/{offset}");
            }

            Value = new byte[Length];
            Array.Copy(buffer, offset, Value, 0, Length);
        }

        public string ToBase32()
        {
            return Value.BytesToBase32(ignorePartialSymbol: true);
        }

        public string ToHex()
        {
            return Convert.ToHexString(Value);
        }

        public override bool Equals(object other)
        {
            if (other is Address address)
            {
                return Equals(address);
            }
            return false;
        }

        public bool Equals(Address other)
        {
            for (int i = 0; i < Length; i += 8)
            {
                if (BitConverter.ToInt64(Value, i) != BitConverter.ToInt64(other.Value, i))
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            // Not intended to be cryptographically secure.
            // For simplicity, just take first 4 bytes of address, as it is encrypted anyway
            return BitConverter.ToInt32(Value);
        }

        public override string ToString()
        {
            return Convert.ToHexString(Value);
        }
    }
}
