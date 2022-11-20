namespace Wibblr.Grufs
{
    public class KeyEncryptionKey
    {
        public static int Length = 32;

        public byte[] Value { get; init; }

        public KeyEncryptionKey(byte[] value)
        {
            if (value.Length != Length)
            {
                throw new Exception("Invalid key length");
            }

            Value = value;
        }
    }
}
