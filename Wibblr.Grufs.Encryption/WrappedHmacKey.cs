
using System.Diagnostics;

using RFC3394;

namespace Wibblr.Grufs.Encryption
{
    [DebuggerDisplay("{ToString()}")]

    public record struct WrappedHmacKey
    {
        public readonly static int Length = 40;

        internal byte[] Value { get; private init; }

        public WrappedHmacKey(ReadOnlySpan<byte> value)
        {
            if (value.Length != Length)
            {
                throw new ArgumentException($"Invalid wrapped HMAC key length (expected {Length}, was {value.Length})");
            }

            Value = new byte[Length];
            value.CopyTo(Value);
        }

        public HmacKey Unwrap(HmacKeyEncryptionKey kek) => new HmacKey(new RFC3394Algorithm().Unwrap(kek.Value, Value));
        public ReadOnlySpan<byte> ToSpan() => new ReadOnlySpan<byte>(Value);
        public override string ToString() => Convert.ToHexString(Value);
    }
}
