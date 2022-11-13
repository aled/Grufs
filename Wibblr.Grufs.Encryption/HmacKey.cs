
using System.Security.Cryptography;

using RFC3394;

namespace Wibblr.Grufs
{
    public struct HmacKey
    {
        public static int Length = 32;

        public byte[] _value;

        public byte[] Value { 
            get
            {
                return _value;
            }
            set
            {
                if (value.Length != Length)
                {
                    throw new Exception("Invalid key length");
                }

                _value = value;
            }
        }

        public HmacKey(byte[] value)
        {
            if (value.Length != Length)
            {
                throw new Exception("Invalid key length");
            }

            _value = value;
        }

        public HmacKey(HmacKeyEncryptionKey kek, WrappedHmacKey key)
        {
            _value = new RFC3394Algorithm().Unwrap(kek.Value, key.Value);

            if (_value.Length != Length)
            {
                throw new Exception("Invalid key length");
            }
        }
    }
}
