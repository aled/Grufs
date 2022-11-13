using System.Text;

using FluentAssertions;

namespace Wibblr.Base32.Tests
{
    public static class Extensions
    {
        public static byte[] BinaryToBytes(this string s)
        {
            return s
                .Where(c => c == '0' || c == '1')
                .Chunk(8)
                .Select(s => Convert.ToByte(new string(s), 2))
                .ToArray();
        }

        public static string BinaryToBase32(this string s, bool ignorePartialSymbols = false)
        {
            return s
                .BinaryToBytes()
                .BytesToBase32(ignorePartialSymbols);
        }

        public static string Right(this string s, int i)
        {
            return s.Substring(s.Length - i, i);
        }
    }

    public class Base32Tests
    {
        [Fact]
        public void ConvertSymbol()
        {
            var symbols = Enumerable.Range('0', 10)
                .Concat(Enumerable.Range('a', 26))
                .Except(new int[] { 'b', 'i', 'l', 'o' })
                .Select(x => (char)x);

            int i = 0;
            foreach (var symbol in symbols)
            {
                symbol.Should().Be(Base32.Symbols[i]);
                Base32.SymbolToIndex(symbol).Should().Be(i);
                i++;
            }
        }

        [Fact]
        public void ConvertBytesToBase32()
        {
            "000_00000".BinaryToBase32().Should().Be("00");
            "000_00000".BinaryToBase32(true).Should().Be("0");
            "000_00001".BinaryToBase32().Should().Be("01");
            "000_00001".BinaryToBase32(true).Should().Be("1");
            "000_11111".BinaryToBase32().Should().Be("0z");
            "000_11111".BinaryToBase32(true).Should().Be("z");
            "001_00000".BinaryToBase32().Should().Be("10");
            "001_00000".BinaryToBase32(true).Should().Be("0");
            "001_11111".BinaryToBase32().Should().Be("1z");
            "001_11111".BinaryToBase32(true).Should().Be("z");
            "111_00000".BinaryToBase32().Should().Be("70");
            "111_00000".BinaryToBase32(true).Should().Be("0");
        
            "0_00011_00010_00001".BinaryToBase32().Should().Be("0321");
            "0_00011_00010_00001".BinaryToBase32(true).Should().Be("321");

            "1_00110_00101_00100".BinaryToBase32().Should().Be("1654");
            "1_00110_00101_00100".BinaryToBase32(true).Should().Be("654");

            "11000_10111_10110_10101_10100_10011_10010_10001".BinaryToBase32().Should().Be("srqpnmkj");
            "11000_10111_10110_10101_10100_10011_10010_10001".BinaryToBase32(true).Should().Be("srqpnmkj");

            "111_11011_11010_11001_11110_11101_11100_11011_11010_11001".BinaryToBase32().Should().Be("7vutyxwvut");
            "111_11011_11010_11001_11110_11101_11100_11011_11010_11001".BinaryToBase32(true).Should().Be("vutyxwvut");
        }

        private int GetExpectedSymbolCount(int bitCount, bool ignorePartial) 
        {
            return ignorePartial 
                ? bitCount switch
                {
                    40 => 8,
                    > 35 => 7,
                    > 30 => 6,
                    > 25 => 5,
                    > 20 => 4,
                    > 15 => 3,
                    > 10 => 2,
                    > 5 => 1,
                    > 0 => 0,
                    _ => throw new Exception()
                }
                : bitCount switch
                {
                    40 => 8,
                    > 35 => 8,
                    > 30 => 7,
                    > 25 => 6,
                    > 20 => 5,
                    > 15 => 4,
                    > 10 => 3,
                    > 5 => 2,
                    > 0 => 1,
                    _ => throw new Exception()
                };
        }

        private int GetExpectedByteCount(int bitCount, bool ignorePartial)
        {
            return ignorePartial
                ? bitCount switch
                {
                    40 => 5,
                    > 32 => 4,
                    > 24 => 3,
                    > 16 => 2,
                    > 8 => 1,
                    > 0 => 0,
                    _ => throw new Exception()
                }
                : bitCount switch
                {
                    40 => 5,
                    > 32 => 5,
                    > 24 => 4,
                    > 16 => 3,
                    > 8 => 2,
                    > 0 => 1,
                    _ => throw new Exception()
                };
        }

        [Theory]
        [InlineData("0000_01010_01001_01000_00111", "0a987")]
        public void RoundTrip(string binary, string base32)
        {
            int bitCount;
            int expectedSymbolCount;
            int expectedSymbolCountIgnorePartial;
            int expectedByteCount;
            int expectedByteCountIgnorePartial;

            byte[] bytes = binary.BinaryToBytes();
            bitCount = bytes.Length * 8;

            // First convert bytes to base 32, both with and without partial symbols
            var actualBase32 = binary.BinaryToBase32();
            expectedSymbolCount = GetExpectedSymbolCount(bitCount, false);
            actualBase32.Length.Should().Be(expectedSymbolCount);
            actualBase32.Should().Be(base32);

            var actualBase32IgnorePartial = binary.BinaryToBase32(true);
            expectedSymbolCountIgnorePartial = GetExpectedSymbolCount(bitCount, true);
            actualBase32IgnorePartial.Length.Should().Be(expectedSymbolCountIgnorePartial);
            actualBase32IgnorePartial.Should().Be(base32.Right(expectedSymbolCountIgnorePartial));

            // Now convert back to binary, with and without partial bytes
            bitCount = actualBase32.Length * 5;
            expectedByteCount = GetExpectedByteCount(bitCount, false);
            expectedByteCountIgnorePartial = GetExpectedByteCount(bitCount, true);

            var roundtripBytes = actualBase32.Base32ToBytes();
            roundtripBytes.Length.Should().Be(expectedByteCount);

            // Converting bytes to base32 (keeping partial symbols) will increase the number of bits
            // up to the next multiple of 5 (up to 4 bits added)


            //roundtripBytes.Should().BeEquivalentTo


            //roundtripBytes.Base32ToBytes(true).Length.Should().Be(expectedByteCountIgnorePartial);


            "0001_01010_01001_01000_00111".BinaryToBase32().Should().Be("1a987");
            "0010_01010_01001_01000_00111".BinaryToBase32().Should().Be("2a987");
            "0100_01010_01001_01000_00111".BinaryToBase32().Should().Be("4a987");
            "1000_01010_01001_01000_00111".BinaryToBase32().Should().Be("8a987");

            "0000_01010_01001_01000_00111".BinaryToBase32(true).Should().Be("a987");

            "00_10000_01111_01110_01101_01100_01011".BinaryToBase32().Should().Be("0hgfedc");

            "11000_10111_10110_10101_10100_10011_10010_10001".BinaryToBase32().Should().Be("srqpnmkj");
            "11000_10111_10110_10101_10100_10011_10010_10001".BinaryToBase32(true).Should().Be("srqpnmkj");

        }
    }
}