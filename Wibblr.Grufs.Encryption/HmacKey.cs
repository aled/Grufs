using System;
using System.Diagnostics;

using RFC3394;

namespace Wibblr.Grufs.Encryption
{
    [DebuggerDisplay("{ToString()}")]
    public record struct HmacKey
    {
        public static readonly int Length = 32;

        internal byte[] Value { get; private init; }

        public HmacKey(ReadOnlySpan<byte> value)
        {
            if (value.Length != Length)
            {
                throw new ArgumentException($"Invalid HMAC key length (expected {Length}, was {value.Length})");
            }

            Value= new byte[Length];
            value.CopyTo(Value);
        }

        public HmacKey(HmacKeyEncryptionKey kek, WrappedHmacKey key)
        {
            Value = new RFC3394Algorithm().Unwrap(kek.Value, key.Value);

            if (Value.Length != Length)
            {
                throw new Exception("Invalid key length");
            }
        }

        public WrappedHmacKey Wrap(HmacKeyEncryptionKey kek) => new WrappedHmacKey(new RFC3394Algorithm().Wrap(kek.Value, Value));

        public override string ToString() => Convert.ToHexString(Value);
    }
}
