using System.Globalization;
using System.Text;

namespace Wibblr.Grufs.Core
{
    /// <summary>
    /// Simple Key-Value serializer.
    /// 
    /// Serializes kv pairs, one per line into an ASCII file.
    /// key:value
    /// 
    /// where value is string|binary|number|date|bool|null
    ///
    /// string is wrapped in double quotes, and uses backslash as escape character. Valid escapes are \" \\ \r \n \t \x00 \x01 .. \xFF \u0000 .. \uFFFF 
    ///
    /// Keys must be strings and cannot contain the ':' or ' ' characters
    /// </summary>
    public class SkvSerializer
    {
        private static readonly string[] dateTimeFormats = new[]
        {
            "yyyy-MM-dd",
            "yyyy-MM-ddTHH:mm:ssK",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fK",
            "yyyy-MM-ddTHH:mm:ss.f",
            "yyyy-MM-ddTHH:mm:ss.ffK",
            "yyyy-MM-ddTHH:mm:ss.ff",
            "yyyy-MM-ddTHH:mm:ss.fffK",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss.ffffK",
            "yyyy-MM-ddTHH:mm:ss.ffff",
            "yyyy-MM-ddTHH:mm:ss.fffffK",
            "yyyy-MM-ddTHH:mm:ss.fffff",
            "yyyy-MM-ddTHH:mm:ss.ffffffK",
            "yyyy-MM-ddTHH:mm:ss.fffffff",
            "yyyy-MM-ddTHH:mm:ss.fffffffK"
        };

        private string EncodeString(string s)
        {
            // Encode all non-printable characters:
            var sb = new StringBuilder();
            sb.Append('"');
            foreach(var c in s)
            {
                sb.Append(c switch
                {
                    // escaped printable ascii chars
                    '"' => @"\""",
                    '\\' => @"\\",

                    // non-escaped printable ascii chars
                    >= (char)32 and <= (char)126 => c,

                    // special non-printable ascii characters
                    '\n' => @"\n",
                    '\r' => @"\r",
                    '\t' => @"\t",

                    // other non-printable ascii characters
                    <= (char)255 => $@"\x{(int)c:x2}", // \x17, \x18 etc

                    // unicode characters
                    _ => $@"\u{(int)c:x4}",
                });
            }
            sb.Append('"');
            return sb.ToString();
        }

        private string DecodeString(string s)
        {
            var sb = new StringBuilder();

            // exclude quotes at start and end
            int i = 0;
            while (++i < s.Length - 1)
            {
                if (s[i] != '\\')
                {
                    sb.Append(s[i]);
                    continue;
                }
                else
                {
                    i++;
                    if (s[i] == '"' || s[i] == '\\') sb.Append(s[i]);
                    else if (s[i] == 'n') sb.Append('\n');
                    else if (s[i] == 'r') sb.Append('\r');
                    else if (s[i] == 't') sb.Append('\t');
                    else if (s[i] == 'x')
                    {
                        i++;
                        var c = (char)int.Parse(s.Substring(i, 2), NumberStyles.HexNumber);
                        sb.Append(c);
                        i++;
                    }
                    else if (s[i] == 'u')
                    {
                        i++;
                        var c = (char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber);
                        sb.Append(c);
                        i+=3;
                    }
                }
            }
            return sb.ToString();
        }

        public string Serialize(List<KeyValuePair<string, object>> items)
        {
            var sb = new StringBuilder();
            foreach(var item in items)
            {
                if (item.Key.StartsWith(' ') || item.Key.Contains(":"))
                {
                    throw new Exception($"Invalid key: '{item.Key}'");
                }
                sb.Append(item.Key);
                sb.Append(":");

                sb.Append(item.Value switch
                {
                    null => "null",
                    string s => EncodeString(s),
                    DateTime dt => dt.ToString("O"),
                    bool b => b.ToString().ToLower(),
                    int i => i.ToString(),
                    byte[] bytes => "0x" + Convert.ToHexString(bytes),
                    _ => EncodeString(item.Value?.ToString() ?? "null"),
                });

                sb.Append('\n');
            }
            return sb.ToString();
        }

        public List<KeyValuePair<string, object?>> Deserialize(string s)
        {
            var lines = s.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var pairs = new List<KeyValuePair<string, object?>>();

            foreach(var line in lines) 
            {
                var kv = line.Split(':', 2);
                var key = kv[0];
                var value = kv[1];

                object? outValue = null;

                if (value.StartsWith("\"") && value.EndsWith("\""))
                {
                    outValue = DecodeString(value);
                }
                else if (value == "null")
                {
                    outValue = null;
                }
                else if (value == "true")
                {
                    outValue = true;
                }
                else if (value == "false")
                {
                    outValue = false;
                }
                else if (value.StartsWith("0x"))
                {
                    outValue = Convert.FromHexString(value.Substring(2));
                }
                else if (int.TryParse(value, out int i))
                {
                    outValue = i;
                }
                else if (DateTime.TryParseExact(value, dateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d))
                {
                    outValue = d;
                }
                else
                {
                    throw new Exception($"Unable to deserialize value '{value}'");
                }

                pairs.Add(new KeyValuePair<string, object?>(key, outValue));
            }

            return pairs;
        }
    }
}
