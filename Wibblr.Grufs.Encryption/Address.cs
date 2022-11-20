using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Wibblr.Base32;

namespace Wibblr.Grufs
{
    [DebuggerDisplay("{ToString()}")]
    public struct Address
    {
        public static int Length = 32;

        private byte[] _value;

        public Address(byte[] value)
        {
            if (value.Length != Length)
            {
                throw new Exception($"Invalid address length (expected {Length}; actual {value.Length}");
            }

            _value = value;
        }

        public Address(byte[] buffer, int offset = 0)
        {
            if (buffer.Length - offset < Length)
            {
                throw new Exception($"Invalid address length/offset (expected {Length}; actual {buffer.Length}/{offset}");
            }

            _value = new byte[Length];
            Array.Copy(buffer, offset, _value, 0, Length);
        }

        public byte[] Value
        {
            get
            {
                return _value;
            }
        }

        public string ToBase32()
        {
            return _value.BytesToBase32(ignorePartialSymbol: true);
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
                if (BitConverter.ToInt64(_value, i) != BitConverter.ToInt64(other.Value, i))
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            // for simplicity, just take first 4 bytes of address, as it is encrypted anyway
            return BitConverter.ToInt32(_value);
        }

        public override string ToString()
        {
            return Convert.ToHexString(_value);
        }
    }
}
