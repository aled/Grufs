
using System.Security.Cryptography;

using RFC3394;

namespace Wibblr.Grufs
{
    public struct EncryptionKey
    {
        public static int Length = 32;

        public byte[] _value;

        public byte[] Value { 
            get
            {
                return _value;
            }
        }

        public EncryptionKey(byte[] value)
        {
            if (value.Length != Length)
            {
                throw new Exception($"Invalid key length (expected {Length}; actual {value.Length}");
            }

            _value = value;
        }

        public EncryptionKey(KeyEncryptionKey keyEncryptionKey, WrappedKey wrappedKey)
        {
            _value = new RFC3394Algorithm().Unwrap(keyEncryptionKey.Value, wrappedKey.Value);

            if (_value.Length != Length)
            {
                throw new Exception($"Invalid key length (expected {Length}; actual {_value.Length}");
            }
        }

        public static EncryptionKey Random()
        {
            return new EncryptionKey(RandomNumberGenerator.GetBytes(Length));
        }
    }
}
