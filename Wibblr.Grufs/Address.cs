﻿using System.Diagnostics;
using System.Runtime.Intrinsics;

namespace Wibblr.Grufs
{
    [DebuggerDisplay("{ToString()}")]
    public record struct Address
    {
        public static readonly int Length = 32;

        public byte[] _value { get; init; } = new byte[Length];

        public Address(ReadOnlySpan<byte> value)
        {
            ArgumentNullException.ThrowIfNull("value");

            if (value.Length != Length)
            {
                throw new ArgumentException($"Invalid address length (expected {Length}, was {value.Length}");
            }

            value.CopyTo(_value);
        }

        public bool Equals(Address other) => Vector256.EqualsAll(Vector256.Create(_value), Vector256.Create(other._value));

        public override int GetHashCode() => BitConverter.ToInt32(_value);

        public ReadOnlySpan<byte> ToSpan() => new ReadOnlySpan<byte>(_value);

        public override string ToString() => Convert.ToHexString(_value);
    }
}
