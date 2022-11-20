
using System.Diagnostics;

namespace Wibblr.Grufs
{
    public struct WrappedHmacKey
    {
        public static int Length = 40;

        public byte[] _value;

        public byte[] Value
        {
            get
            {
                return _value;
            }
        }

        public WrappedHmacKey(byte[] value)
        {
            if (value.Length != Length)
            {
                throw new Exception("Invalid wrapped key length");
            }

            _value = value;
        }

        public WrappedHmacKey(byte[] buffer, int offset)
        {
            if (buffer.Length - offset < Length)
            {
                throw new Exception("Invalid wrapped key length");
            }

            _value = new byte[Length];
            Array.Copy(buffer, offset, _value, 0, Length);
        }

        public WrappedHmacKey(HmacKeyEncryptionKey kek, HmacKey key)
        {
            _value = new RFC3394.RFC3394Algorithm().Wrap(kek.Value, key.Value);

            Debug.Assert(_value.Length == Length);
        }
    }
}
