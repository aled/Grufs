namespace Wibblr.Grufs
{
    public struct KeyEncryptionKey
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

        public KeyEncryptionKey(byte[] value)
        {
            if (value.Length != Length)
            {
                throw new Exception("Invalid key length");
            }

            _value = value;
        }
    }
}
