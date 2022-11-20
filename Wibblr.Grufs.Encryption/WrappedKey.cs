using System.Diagnostics;

using RFC3394;

namespace Wibblr.Grufs
{
    public class WrappedKey
    {
        public static readonly int Length = 40;
        
        public byte[] Value { get; init; }

        public WrappedKey(byte[] buffer, int offset = 0)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (buffer.Length - offset < Length)
            {
                throw new ArgumentException("Invalid wrapped key length");
            }

            Value = new byte[Length];
            Array.Copy(buffer, offset, Value, 0, Length);
        }

        public EncryptionKey Unwrap(KeyEncryptionKey keyEncryptionKey)
        {
            return new EncryptionKey(new RFC3394Algorithm().Unwrap(keyEncryptionKey.Value, Value));
        }
    }
}
