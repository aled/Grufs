namespace Wibblr.Grufs.Tests
{
    public static class StringExtensions
    {
        public static string Repeat(this string s, int count)
        {
            return string.Create(s.Length * count, s, (chars, s) =>
            {
                for (int i = 0; i < s.Length * count; i++)
                {
                    chars[i] = s[i % s.Length];
                };
            });
        }

        public static byte[] ToBytes(this string hex) 
        {
            return Convert.FromHexString(hex);
        }
    }
}
