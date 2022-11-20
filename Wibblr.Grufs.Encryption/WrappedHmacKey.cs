
using System.Diagnostics;

using RFC3394;

namespace Wibblr.Grufs
{
    public class WrappedHmacKey
    {
        public static int Length = 40;

        public byte[] Value { get; init; }

        public WrappedHmacKey(byte[] value)
        {
            if (value.Length != Length)
            {
                throw new Exception("Invalid wrapped key length");
            }

            Value = value;
        }

        public WrappedHmacKey(byte[] buffer, int offset)
        {
            if (buffer.Length - offset < Length)
            {
                throw new Exception("Invalid wrapped key length");
            }

            Value = new byte[Length];
            Array.Copy(buffer, offset, Value, 0, Length);
        }

        public HmacKey Unwrap(HmacKeyEncryptionKey kek)
        {
            return new HmacKey(new RFC3394Algorithm().Unwrap(kek.Value, Value));
        }
    }
}
