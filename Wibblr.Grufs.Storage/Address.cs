using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.Intrinsics;

namespace Wibblr.Grufs.Storage
{
    [DebuggerDisplay("{ToString()}")]
    public record struct Address
    {
        public static readonly int Length = 32;

        public ImmutableArray<byte> Value { get; private set; }

        public Address()
        {
            throw new NotImplementedException();
        }

        public Address(ReadOnlySpan<byte> value)
        {
            ArgumentNullException.ThrowIfNull("value");

            if (value.Length != Length)
            {
                throw new ArgumentException($"Invalid address length (expected {Length}, was {value.Length}");
            }

            Value = value.ToImmutableArray();
        }

        public bool Equals(Address other) => Vector256.EqualsAll(Vector256.Create(ToSpan()), Vector256.Create(other.ToSpan()));

        public override int GetHashCode() => BitConverter.ToInt32(ToSpan());

        public ReadOnlySpan<byte> ToSpan() => Value.AsSpan();

        public override string ToString() => Convert.ToHexString(ToSpan());
    }
}
