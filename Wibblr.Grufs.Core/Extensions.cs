using System;
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
    }
}
