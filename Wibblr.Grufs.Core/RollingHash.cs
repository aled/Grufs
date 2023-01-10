using System;

namespace Wibblr.Grufs
{
    /// <summary>
    /// A Rolling hash can be updated by moving a window one byte at a time over the input data.
    /// This implementation uses the Rabin algorithm, with parameters that yield a checksum with a range of 0 to 1677212 (i.e. between 23 and 24 bits)
    /// </summary>
    public struct RollingHash
    {
        // constants used in the algorithm
        private static readonly uint modulus = 16777213; // The highest possible prime that doesn't overflow an unsigned int when left-shifted by 1 byte
        private static readonly uint byteRemovalMultiplier = 1;

        public static readonly int WindowSize = 64;
        public uint Value { get; private set; } = 0;

        static RollingHash()
        {
            for (int i = 0; i < WindowSize - 1; i++)
            {
                byteRemovalMultiplier = (byteRemovalMultiplier << 8) % modulus;
            }
        }

        public RollingHash(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length != WindowSize)
            {
                throw new ArgumentException("Invalid byte count for rolling hash initialization");
            }

            foreach (var b in bytes)
            {
                Value = ((Value << 8) + b) % modulus;
            }
            //Console.WriteLine("initial hash " + hash);
        }

        public void Append(byte oldByte, byte newByte)
        {
            Value = (Value + modulus - ((oldByte * byteRemovalMultiplier) % modulus)) % modulus; // remove old byte, ensuring the intermediate result can never go negative.
            Value = ((Value << 8) + newByte) % modulus; // add new byte
        }
    }
}
