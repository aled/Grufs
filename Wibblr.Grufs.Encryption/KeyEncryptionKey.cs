using System.Diagnostics;

namespace Wibblr.Grufs.Encryption
{
    [DebuggerDisplay("{ToString()}")]
    public record struct KeyEncryptionKey
    {
        public static readonly int Length = 32;

        internal byte[] Value { get; private init; }

        public KeyEncryptionKey(ReadOnlySpan<byte> value)
        {
            if (value.Length != Length)
            {
                throw new ArgumentException($"Invalid key encryption key length (expected {Length}, was {value.Length})");
            }
            Value = new byte[Length];
            value.CopyTo(Value);
        }

        public override string ToString() => Convert.ToHexString(Value);
    }
}
