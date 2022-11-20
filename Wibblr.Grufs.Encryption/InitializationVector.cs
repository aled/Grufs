
using System.Security.Cryptography;

namespace Wibblr.Grufs
{
    public class InitializationVector
    {
        public static int Length = 16;
        public byte[] Value { get; init; }

        public InitializationVector(byte[] value)
        {
            if (value.Length != Length)
            {
                throw new Exception($"Invalid IV length (expected {Length}; actual {value.Length}");
            }

            Value = value;
        }

        public InitializationVector(byte[] buffer, int offset)
        {
            if (buffer.Length - offset < Length)
            { 
                throw new Exception("Invalid IV length");
            }

            Value = new byte[Length];
            Array.Copy(buffer, offset, Value, 0, Length);
        }
        
        public static InitializationVector Random()
        {
            return new InitializationVector(RandomNumberGenerator.GetBytes(Length));
        }
    }
}
