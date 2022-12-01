using System;
using System.Collections.Generic;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Wibblr.Base32.Tests")]

namespace Wibblr.Base32
{
    public static class Extensions
    {
        public static string BytesToBase32(this IList<byte> bytes, bool ignorePartialSymbol = false) => Base32.ToString(bytes, ignorePartialSymbol);

        public static byte[] Base32ToBytes(this string base32, bool ignorePartialSymbol = false) => Base32.ToBytes(base32, ignorePartialSymbol);
    }

    /// <summary>
    /// Convert between byte arrays and base-32 strings.
    /// 
    /// The 32 symbols used are the ascii digits and lowercase letters except 'b', 'i', 'l' and 'o'
    /// </summary>
    public static class Base32
    {
        internal static readonly char[] Symbols = "0123456789acdefghjkmnpqrstuvwxyz".ToCharArray();

        internal static int SymbolToIndex(char symbol)
        {
            if (symbol >= '0' && symbol <= '9')
                return symbol - '0';

            if (symbol == 'a')
                return symbol - 'a' + 10;

            if (symbol >= 'c' && symbol <= 'h')
                return symbol - 'c' + 11;

            if (symbol >= 'j' && symbol <= 'k')
                return symbol - 'j' + 17;

            if (symbol >= 'm' && symbol <= 'n')
                return symbol - 'm' + 19;

            if (symbol >= 'p' && symbol <= 'z')
                return symbol - 'p' + 21;

            throw new Exception($"Invalid base-32 symbol '{symbol}'");
        }

        /// <summary>
        /// Convert a base-32 string to a byte array.
        /// </summary>
        /// <param name="base32string">String containing only ascii digits and lowercase letters except 'b', 'i', 'l' and 'o', i.e. 0123456789acdefghjkmnpqrstuvwxyz</param>
        /// <returns>Byte array containing the decoded value</returns>
        public static byte[] ToBytes(string base32string, bool ignorePartialByte = false)
        {
            var bits = base32string.Length * 5;
            int byteCount = 1 + (bits - 1) / 8;

            if (ignorePartialByte && base32string.Length % 8 != 0)
            {
                byteCount--;
            }

            var bytes = new byte[byteCount];
            var byteIndex = byteCount - 1;

            // Process blocks of 8 base-32 symbols, counting from the end.
            // Each block of 8 symbols represents 5 bytes.
            int i, v0, v1, v2, v3, v4, v5, v6, v7;
            for (i = base32string.Length - 1; i >= 7; i -= 8)
            {
                v7 = SymbolToIndex(base32string[i - 0]);
                v6 = SymbolToIndex(base32string[i - 1]);
                v5 = SymbolToIndex(base32string[i - 2]);
                v4 = SymbolToIndex(base32string[i - 3]);
                v3 = SymbolToIndex(base32string[i - 4]);
                v2 = SymbolToIndex(base32string[i - 5]);
                v1 = SymbolToIndex(base32string[i - 6]);
                v0 = SymbolToIndex(base32string[i - 7]);

                bytes[byteIndex--] = (byte)(((v7 & 0b00011111) >> 0) | ((v6 & 0b00000111) << 5));
                bytes[byteIndex--] = (byte)(((v6 & 0b00011000) >> 3) | ((v5 & 0b00011111) << 2) | ((v4 & 0b00000001) << 7));
                bytes[byteIndex--] = (byte)(((v4 & 0b00011110) >> 1) | ((v3 & 0b00001111) << 4));
                bytes[byteIndex--] = (byte)(((v3 & 0b00010000) >> 4) | ((v2 & 0b00011111) << 1) | ((v1 & 0b00000001) << 8));
                bytes[byteIndex--] = (byte)(((v1 & 0b00011110) >> 1) | ((v0 & 0b00011111) << 3));
            }

            // Finally, process the initial block of 1 to 7 chars
            v7 = i >= 0 ? SymbolToIndex(base32string[i - 0]) : 0;
            v6 = i >= 1 ? SymbolToIndex(base32string[i - 1]) : 0;
            v5 = i >= 2 ? SymbolToIndex(base32string[i - 2]) : 0;
            v4 = i >= 3 ? SymbolToIndex(base32string[i - 3]) : 0;
            v3 = i >= 4 ? SymbolToIndex(base32string[i - 4]) : 0;
            v2 = i >= 5 ? SymbolToIndex(base32string[i - 5]) : 0;
            v1 = i >= 6 ? SymbolToIndex(base32string[i - 6]) : 0;

            if (byteIndex >= 0) bytes[byteIndex--] = (byte)(((v7 & 0b00011111) >> 0) | ((v6 & 0b00000111) << 5));
            if (byteIndex >= 0) bytes[byteIndex--] = (byte)(((v6 & 0b00011000) >> 3) | ((v5 & 0b00011111) << 2) | ((v4 & 0b00000001) << 7));
            if (byteIndex >= 0) bytes[byteIndex--] = (byte)(((v4 & 0b00011110) >> 1) | ((v3 & 0b00001111) << 4));
            if (byteIndex >= 0) bytes[byteIndex--] = (byte)(((v3 & 0b00010000) >> 4) | ((v2 & 0b00011111) << 1) | ((v1 & 0b00000001) << 8));
            if (byteIndex >= 0) bytes[byteIndex--] = (byte)(((v1 & 0b00011110) >> 1));

            return bytes;
        }

        public static string ToString(IList<byte> bytes, bool ignorePartialSymbol = false)
        {
            var bits = bytes.Count * 8;
            var symbolCount = 1 + (bits - 1) / 5;

            if (ignorePartialSymbol && (bytes.Count % 5) != 0)
            {
                symbolCount--;
            }

            return string.Create(symbolCount, bytes, (chars, bytes) =>
            {
                var charIndex = symbolCount - 1;

                // Process blocks of 5 bytes, counting from the end.
                // Each block of 5 bytes represents 8 base-32 symbols.
                //
                //                                              |--v0---| |-- v1---| |--v2---| |---v3---| |--v4---| <--- bytes
                // 00000000 11111111 00000000 11111111 00000000 11111 111 00 00000 0 1111 1111 0 00000 00 111 11111
                //                                              |s0-| |-s1-| |s2-| |-s3-| |-s4-| |s5-| |-s6-| |s7-| <--- symbols
                int i, v0, v1, v2, v3, v4;
                for (i = bytes.Count - 1; i >= 4;  i -= 5)
                {
                    v4 = bytes[i - 0];
                    v3 = bytes[i - 1];
                    v2 = bytes[i - 2];
                    v1 = bytes[i - 3];
                    v0 = bytes[i - 4];

                    chars[charIndex--] = Symbols[(v4 & 0b00011111)];                               // s7
                    chars[charIndex--] = Symbols[(v3 & 0b00000011) << 3 | (v4 & 0b11100000) >> 5]; // s6
                    chars[charIndex--] = Symbols[(v3 & 0b01111100) >> 2];                          // s6
                    chars[charIndex--] = Symbols[(v2 & 0b00001111) << 1 | (v3 & 0b10000000) >> 7]; // s4
                    chars[charIndex--] = Symbols[(v1 & 0b00000001) << 4 | (v2 & 0b11110000) >> 4]; // s3
                    chars[charIndex--] = Symbols[(v1 & 0b00111110) >> 1];                          // s2
                    chars[charIndex--] = Symbols[(v0 & 0b00000111) << 2 | (v1 & 0b11000000) >> 6]; // s1
                    chars[charIndex--] = Symbols[(v0 & 0b11111000) >> 3];                          // s0
                }

                // Finally, process the initial block of 1 to 4 bytes. Same logic as above, but missing bytes are given
                // the value zero, and stop when the required number of symbols have been output.
                v4 = i >= 0 ? bytes[i - 0] : 0;
                v3 = i >= 1 ? bytes[i - 1] : 0;
                v2 = i >= 2 ? bytes[i - 2] : 0;
                v1 = i >= 3 ? bytes[i - 3] : 0;

                if (charIndex < 0) return; chars[charIndex--] = Symbols[(v4 & 0b00011111)];
                if (charIndex < 0) return; chars[charIndex--] = Symbols[(v3 & 0b00000011) << 3 | (v4 & 0b11100000) >> 5];
                if (charIndex < 0) return; chars[charIndex--] = Symbols[(v3 & 0b01111100) >> 2];
                if (charIndex < 0) return; chars[charIndex--] = Symbols[(v2 & 0b00001111) << 1 | (v3 & 0b10000000) >> 7];
                if (charIndex < 0) return; chars[charIndex--] = Symbols[(v1 & 0b00000001) << 4 | (v2 & 0b11110000) >> 4];
                if (charIndex < 0) return; chars[charIndex--] = Symbols[(v1 & 0b00111110) >> 1];
                if (charIndex < 0) return; chars[charIndex--] = Symbols[(v1 & 0b11000000) >> 6];
            });
        }
    }
}