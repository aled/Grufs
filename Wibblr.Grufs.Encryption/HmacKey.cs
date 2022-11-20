using System.Diagnostics;
using System.Security.Cryptography;

using RFC3394;

namespace Wibblr.Grufs
{
    public class HmacKey
    {
        public static int Length = 32;

        public byte[] Value { get; init; }

        public HmacKey(byte[] value)
        {
            if (value.Length != Length)
            {
                throw new Exception("Invalid key length");
            }

            Value = value;
        }

        public HmacKey(HmacKeyEncryptionKey kek, WrappedHmacKey key)
        {
            Value = new RFC3394Algorithm().Unwrap(kek.Value, key.Value);

            if (Value.Length != Length)
            {
                throw new Exception("Invalid key length");
            }
        }

        public WrappedHmacKey Wrap(HmacKeyEncryptionKey kek)
        {
            return new WrappedHmacKey(new RFC3394Algorithm().Wrap(kek.Value, Value));
        }
    }
}
