using System.Diagnostics;

using RFC3394;

namespace Wibblr.Grufs.Encryption
{
    [DebuggerDisplay("{ToString()}")]
    public record struct WrappedEncryptionKey
    {
        public static readonly int Length = 40;
        
        internal byte[] Value { get; private init; }

        public WrappedEncryptionKey(ReadOnlySpan<byte> value)
        {
            if (value == null || value.Length != Length)
            {
                throw new ArgumentException($"Invalid wrapped key length (expected {Length}, was {value.Length})");
            }

            Value = new byte[Length];
            value.CopyTo(Value);
        }

        public EncryptionKey Unwrap(KeyEncryptionKey keyEncryptionKey)
        {
            return new EncryptionKey(new RFC3394Algorithm().Unwrap(keyEncryptionKey.Value, Value));
        }

        public ReadOnlySpan<byte> ToSpan() => new ReadOnlySpan<byte>(Value);

        public override string ToString() => Convert.ToHexString(Value);
    }
}
