
using System.Diagnostics;

using RFC3394;

namespace Wibblr.Grufs.Encryption
{
    [DebuggerDisplay("{ToString()}")]

    public record struct WrappedHmacKey
    {
        public readonly static int Length = 40;

        internal byte[] Value { get; private init; }

        public WrappedHmacKey(byte[] value)
        {
            if (value.Length != Length)
            {
                throw new ArgumentException($"Invalid wrapped HMAC key length (expected {Length}, was {value.Length})");
            }

            Value = value;
        }

        public WrappedHmacKey(byte[] buffer, int offset)
        {
            if (buffer.Length - offset < Length)
            {
                throw new Exception("Invalid wrapped key length");
            }

            Value = new byte[Length];
            Array.Copy(buffer, offset, Value, 0, Length);
        }

        public HmacKey Unwrap(HmacKeyEncryptionKey kek) => new HmacKey(new RFC3394Algorithm().Unwrap(kek.Value, Value));

        public override string ToString() => Convert.ToHexString(Value);
    }
}
