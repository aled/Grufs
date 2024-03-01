namespace Wibblr.Grufs.Storage
{
    public static class FileStorageUtils
    {
        public static string GeneratePath(Address address)
        {
            // return path of the form
            //   ab\cd\abcdef...
            var hex = address.ToString();
            var length = 6 + hex.Length;

            return string.Create(length, hex, (chars, hex) =>
            {
                chars[0] = hex[0];
                chars[1] = hex[1];
                chars[2] = Path.DirectorySeparatorChar;
                chars[3] = hex[2];
                chars[4] = hex[3];
                chars[5] = Path.DirectorySeparatorChar;

                hex.AsSpan().CopyTo(chars.Slice(6));
            });
        }

        public static bool IsHexString(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            if (s.Length % 2 != 0)
            {
                return false;
            }

            foreach (var c in s)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z'))
                {
                    continue;
                }

                return false;
            }

            return true;
        }
    }
}
