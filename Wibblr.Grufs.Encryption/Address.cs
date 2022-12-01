using System;
using System.Diagnostics;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;

namespace Wibblr.Grufs.Encryption
{
    [DebuggerDisplay("{ToString()}")]
    public record struct Address
    {
        public static readonly int Length = 32;

        internal byte[] Value { get; private init; }

        public Address(ReadOnlySpan<byte> value)
        {
            if (value.Length != Length)
            {
                throw new ArgumentException($"Invalid length (expected {Length}, was {value.Length})");
            }

            Value = new byte[Length];
            value.CopyTo(Value);
        }

        public Address(HmacKey hmacKey, byte[] buffer, int offset, int count)
        {
            Value = new HMACSHA256(hmacKey.Value).ComputeHash(buffer, offset, count);
        }

        public ReadOnlySpan<byte> ToSpan() => new ReadOnlySpan<byte>(Value);

        public bool Equals(Address other) => Vector256.EqualsAll(Vector256.Create(Value), Vector256.Create(other.Value));

        public override int GetHashCode() => BitConverter.ToInt32(Value);

        public override string ToString() => Convert.ToHexString(Value);
    }
}
