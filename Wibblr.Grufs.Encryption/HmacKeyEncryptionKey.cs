namespace Wibblr.Grufs
{
    public class HmacKeyEncryptionKey
    {
        public static int Length = 32;

        public byte[] Value { get; init;
        }

        public HmacKeyEncryptionKey(byte[] value)
        {
            if (value.Length != Length)
            {
                throw new Exception("Invalid key length");
            }
            Value = value;
        }
    }
}
