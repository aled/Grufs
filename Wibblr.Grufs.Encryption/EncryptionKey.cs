
using System.Diagnostics;
using System.Security.Cryptography;

using RFC3394;

namespace Wibblr.Grufs.Encryption
{
    [DebuggerDisplay("{ToString()}")]
    public record struct EncryptionKey
    {
        public static readonly int Length = 32;

        internal byte[] Value { get; private init; }

        public static EncryptionKey Random()
        {
            return new EncryptionKey(RandomNumberGenerator.GetBytes(Length));
        }

        public EncryptionKey(ReadOnlySpan<byte> value)
        {
            if (value.Length != Length)
            {
                throw new ArgumentException($"Invalid key length (expected {Length}, was {value.Length})");
            }

            Value = new byte[Length];
            value.CopyTo(Value);
        }

        public WrappedEncryptionKey Wrap(KeyEncryptionKey kek)
        {
            return new WrappedEncryptionKey(new RFC3394Algorithm().Wrap(kek.Value, Value));
        }

        public override string ToString() => Convert.ToHexString(Value);
    }
}
