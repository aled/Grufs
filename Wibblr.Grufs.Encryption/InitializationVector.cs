
using System.Security.Cryptography;

namespace Wibblr.Grufs
{
    public struct InitializationVector
    {
        public static int Length = 16;

        public byte[] _value;

        public byte[] Value { 
            get
            {
                return _value;
            }
        }

        public InitializationVector(byte[] value)
        {
            if (value.Length != Length)
            {
                throw new Exception($"Invalid IV length (expected {Length}; actual {value.Length}");
            }

            _value = value;
        }

        public InitializationVector(byte[] buffer, int offset)
        {
            if (buffer.Length - offset < Length)
            { 
                throw new Exception("Invalid IV length");
            }
        
            _value = new byte[16];
            Array.Copy(buffer, offset, _value, 0, Length);
        }
        
        public static InitializationVector Random()
        {
            return new InitializationVector(RandomNumberGenerator.GetBytes(Length));
        }
    }
}
