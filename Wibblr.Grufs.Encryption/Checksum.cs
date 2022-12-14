using System.Diagnostics;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;

namespace Wibblr.Grufs.Encryption
{

    [DebuggerDisplay("{ToString()}")]
    public record struct Checksum
    {
        public static readonly int Length = SHA256.HashSizeInBytes;

        internal byte[] Value { get; private init; }

        private Checksum(byte[] value)
        {
            Value = value;
        }

        public Checksum(ReadOnlySpan<byte> value)
        {
            if (value.Length != Length)
            {
                throw new ArgumentException($"Invalid checksum length (expected {Length}, was {value.Length})");
            }

            Value = new byte[Length];
            value.CopyTo(Value);
        }

        public static Checksum Build(ReadOnlySpan<byte> inputData)
        {
            return new Checksum(SHA256.HashData(inputData));
        }

        public bool Equals(Checksum other) => Vector256.EqualsAll(Vector256.Create(Value), Vector256.Create(other.Value));
        
        public override int GetHashCode() => Value.GetHashCode();

        public ReadOnlySpan<byte> ToSpan() => new ReadOnlySpan<byte>(Value);

        public override string ToString() => Convert.ToHexString(Value);
    }
}
