using System;
using System.Diagnostics;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;

namespace Wibblr.Grufs.Encryption
{
    [DebuggerDisplay("{ToString()}")]
    public record struct Hmac
    {
        public static readonly int Length = 32;

        internal byte[] Value { get; private init; }

        public Hmac(ReadOnlySpan<byte> value)
        {
            if (value.Length != Length)
            {
                throw new ArgumentException($"Invalid HMAC length (expected {Length}, was {value.Length})");
            }

            Value = new byte[Length];
            value.CopyTo(Value);
        }

        public Hmac(HmacKey hmacKey, ReadOnlySpan<byte> buffer)
        {
            Value= new byte[Length];

            if (!new HMACSHA256(hmacKey.Value).TryComputeHash(buffer, Value, out _))
            {
                throw new Exception();
            }
        }

        public Hmac(HmacKey hmacKey, byte[] buffer, int offset = 0, int count = -1)
        {
            Value = new HMACSHA256(hmacKey.Value).ComputeHash(buffer, offset, count);
        }

        public ReadOnlySpan<byte> ToSpan() => new ReadOnlySpan<byte>(Value);

        public override string ToString() => Convert.ToHexString(Value);
    }
}
