
using System.Diagnostics;
using System.Security.Cryptography;

namespace Wibblr.Grufs.Encryption
{
    [DebuggerDisplay("{ToString()}")]
    public record struct InitializationVector
    {
        public static readonly int Length = 16;
        
        internal byte[] Value { get; private init; }

        public InitializationVector(ReadOnlySpan<byte> value)
        {
            if (value.Length != Length)
            {
                throw new ArgumentException($"Invalid IV length (expected {Length}; actual {value.Length}");
            }

            Value = new byte[Length];
            value.CopyTo(Value);
        }

        public static InitializationVector Random()
        {
            return new InitializationVector(RandomNumberGenerator.GetBytes(Length));
        }

        public ReadOnlySpan<byte> ToSpan() => new ReadOnlySpan<byte>(Value);

        public override string ToString() => Convert.ToHexString(Value);
    }
}
