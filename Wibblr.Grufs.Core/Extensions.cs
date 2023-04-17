using System;
using System.Numerics;
using System.Text;

namespace Wibblr.Grufs.Core
{
    public static class Extensions
    {
        public static int GetSerializedLength(this string s)
        {
            int byteCount = Encoding.UTF8.GetByteCount(s);
            return GetSerializedLength(byteCount) + byteCount;
        }

        public static int GetSerializedLength(this int i)
        {
            return new VarInt(i).GetSerializedLength();
        }

        public static int GetSerializedLength(this long i)
        {
            return new VarLong(i).GetSerializedLength();
        }

        public static int GetSerializedLength(this ReadOnlySpan<byte> bytes)
        {
            return GetSerializedLength(bytes.Length) + bytes.Length;
        }

        public static int GetSerializedLength(this byte[] bytes)
        {
            return GetSerializedLength(bytes.Length) + bytes.Length;
        }

        public static (string head, string tail) SplitLast(this string s, char separator)
        {
            var index = s.LastIndexOf(separator);
            if (index == -1)
            {
                return ("", s);
            }
            return (s.Substring(0, index), s.Substring(index + 1));
        }

        public static string Format<T>(this T n, bool human) where T : INumber<T>
        {
            if (human)
            {
                // Human formatting - use 3 significant digits.
                // Above 999 switch to the next SI unit (Ki, Mi, Gi, etc)
                var d = Convert.ToDecimal(n);
                var scaled = Math.Abs(d);

                string[] units = new[] { "", "Ki", "Mi", "Gi", "Ti" };
                int unitIndex = 0;

                while (scaled >= 1000 && unitIndex < units.Length)
                {
                    unitIndex++;
                    scaled /= 1024;
                }

                var decimalPlaces = scaled switch
                {  
                    < 1 => 3,
                    < 10 => 2,
                    < 100 => 1,
                    < 1000 => 1,
                    _ => 0
                };

                var sb = new StringBuilder();
                if (d < 0) sb.Append("-");
                sb.Append(decimal.Round(scaled, decimalPlaces));
                if (unitIndex > 0) sb.Append(" ").Append(units[unitIndex]);

                return sb.ToString();
            }

            return string.Format("{0:n0}", n);
        }
    }
}
