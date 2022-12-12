using System.Diagnostics;
using System.Security.Cryptography;

namespace Wibblr.Grufs.Encryption
{
    [DebuggerDisplay("{ToString()}")]
    public record struct Salt
    {
        public static readonly int Length = 16;

        internal byte[] Value { get; private init; }

        public static Salt Random() => new Salt(RandomNumberGenerator.GetBytes(Length));

        private Salt(byte[] value)
        {
            Value = value;
        }

        public Salt(ReadOnlySpan<byte> value)
        {
            if (value.Length != Length)
            {
                throw new ArgumentException($"Invalid salt length (expected {Length}, was {value.Length})");
            }

            Value = new byte[Length];
            value.CopyTo(Value);
        }

        public ReadOnlySpan<byte> ToSpan() => new ReadOnlySpan<byte>(Value);

        public override string ToString() => Convert.ToHexString(Value);
    }
}
