using System.Diagnostics;

namespace Wibblr.Grufs.Encryption
{
    [DebuggerDisplay("{ToString()}")]
    public record struct HmacKeyEncryptionKey
    {
        public static readonly int Length = 32;

        internal byte[] Value { get; private init; }

        public HmacKeyEncryptionKey(ReadOnlySpan<byte> value)
        {
            if (value.Length != Length)
            {
                throw new ArgumentException($"Invalid HMAC KEK length (expected {Length}, was {value.Length})");
            }

            Value = new byte[Length];
            value.CopyTo(Value);
        }
    }
}
