using System;

namespace Wibblr.Grufs.Core
{
    public static class Extensions
    {
        public static int GetSerializedLength(this string s)
        {
            return GetSerializedLength(s.Length) + (2 * s.Length);
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
