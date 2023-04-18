namespace Wibblr.Grufs.Core
{
    public class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[]? x, byte[]? y)
        {
            if (x == null)
            {
                return y == null;
            }

            if (y == null)
            {
                return x == null;
            }

            if (x.Length != y.Length)
            {
                return false;
            }

            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i])
                {
                    return false;
                }
            }

            return true;
        }

        // This should be faster than Equals() - upon a hash collision, Equals() will be called.
        // Simply hash a prefix and suffix, max 8 bytes each
        public int GetHashCode(byte[] x)
        {
            uint hashCode = 0;

            if (x != null)
            {
                int prefixLen = Math.Min(x.Length, 8);
                int suffixLen = Math.Min(x.Length - prefixLen, 8);

                uint modulus = 16777213;

                // same algorithm as RollingHash
                for (int i = 0; i < prefixLen; i++)
                {
                    hashCode = ((hashCode << 8) + x[i]) % modulus;
                }

                for (int i = x.Length - suffixLen; i < x.Length; i++)
                {
                    hashCode = ((hashCode << 8) + x[i]) % modulus;
                }
            }

            //Console.WriteLine(Convert.ToHexString(x) + " " + hashCode);
            return unchecked((int)hashCode);
        }
    }
}
