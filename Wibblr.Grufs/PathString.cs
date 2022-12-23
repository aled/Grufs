using System.Text;

namespace Wibblr.Grufs
{
    /// <summary>
    /// Represents a validated path. Can be either a directory name, file name, relative or full path name.
    /// 
    /// Both forward and backslashes are directory separators. No drive letters are allowed.
    /// 
    /// The following characters are 
    /// </summary>
    public class PathString
    {
        private string _path { get; init; }
        private byte[] _utf8 { get; init; }

        private static bool isInvalidChar(char c)
        {
            return c <= 31 || c == ':' || c == '*' || c == '?' || c == '\"' || c == '<' || c == '>' || c == '|';
        }

        private IEnumerable<char> EncodedChar(char c)
        {
            if (isInvalidChar(c))
            {
                var hex = Convert.ToHexString(new byte[] { (byte)c });
                yield return '%';
                yield return hex[0];
                yield return hex[1];
            }
            else
            {
                yield return c;
            }
        }

        public PathString(BufferReader reader)
        {
            var length = reader.ReadVarInt().Value;
            _utf8 = reader.ReadBytes(length).ToArray();

            // validate UTF8
            _path = Encoding.UTF8.GetString(_utf8);
        }

        public PathString(string path)
        {
            if (path == null) throw new ArgumentNullException("path");

            _path = path.IsNormalized() ? path : path.Normalize();
            _path = _path.Replace("\\", "/");

            if (_path.Any(isInvalidChar))
            {
                _path = new string(_path.SelectMany(EncodedChar).ToArray());
            }

            _utf8 = Encoding.UTF8.GetBytes(_path);
        }

        public int GetSerializedLength()
        {
            return new VarInt(_utf8.Length).GetSerializedLength() + _utf8.Length;
        }

        public void SerializeTo(BufferBuilder builder)
        {
            builder.CheckBounds(GetSerializedLength());
            builder.AppendVarInt(new VarInt(_utf8.Length));
            builder.AppendBytes(_utf8);
        }

        public override string ToString()
        {
            return _path;
        }
    }
}
