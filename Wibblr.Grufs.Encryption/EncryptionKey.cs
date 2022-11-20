
using System.Diagnostics;
using System.Security.Cryptography;

using RFC3394;

namespace Wibblr.Grufs
{
    public class EncryptionKey
    {
        public static int Length => 32;

        public byte[] Value { get; init; }

        public static EncryptionKey Random()
        {
            return new EncryptionKey(RandomNumberGenerator.GetBytes(Length));
        }

        public EncryptionKey(byte[] value)
        {
            if (value.Length != Length)
            {
                throw new Exception($"Invalid key length (expected {Length}; actual {value.Length}");
            }

            Value = value;
        }

        public WrappedEncryptionKey Wrap(KeyEncryptionKey kek)
        {
            return new WrappedEncryptionKey(new RFC3394Algorithm().Wrap(kek.Value, Value));
        }
    }
}
