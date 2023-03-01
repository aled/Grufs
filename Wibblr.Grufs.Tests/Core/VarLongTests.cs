using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Tests.Core
{
    public class VarLongTests
    {
        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(127, 1)]
        [InlineData(128, 2)] /* 2^7 */
        [InlineData(129, 2)]
        [InlineData(1 << 14 - 1, 2)]
        [InlineData(1 << 14, 3)]
        [InlineData(1 << 14 + 1, 3)]
        [InlineData(1 << 21 - 1, 3)]
        [InlineData(1 << 21, 4)] 
        [InlineData(1 << 21 + 1, 4)]
        [InlineData(1 << 28 - 1, 4)]
        [InlineData(1 << 28, 5)]
        [InlineData(1 << 28 + 1, 5)]
        [InlineData(1L << 35 - 1, 5)]
        [InlineData(1L << 35, 6)]
        [InlineData(1L << 35 + 1, 6)]
        [InlineData(1L << 42 - 1, 6)]
        [InlineData(1L << 42, 7)]
        [InlineData(1L << 42 + 1, 7)]
        [InlineData(1L << 49 - 1, 7)]
        [InlineData(1L << 49, 8)]
        [InlineData(1L << 49 + 1, 8)]
        [InlineData(1L << 56 - 1, 8)]
        [InlineData(1L << 56, 9)]
        [InlineData(1L << 56 + 1, 9)]
        [InlineData(long.MaxValue - 1, 9)]
        [InlineData(long.MaxValue, 9)]
        [InlineData(-1, 9)]
        [InlineData(-1 << 7 - 1, 9)]
        [InlineData(-1 << 7, 9)]
        [InlineData(-1 << 7 + 1, 9)]
        [InlineData(-1 << 14 - 1, 9)]
        [InlineData(-1 << 14, 9)]
        [InlineData(-1 << 14 + 1, 9)]
        [InlineData(-1 << 56 - 1, 9)]
        [InlineData(-1 << 56, 9)]
        [InlineData(-1 << 56 + 1, 9)]
        [InlineData(-1 << 64 - 1, 9)]
        [InlineData(long.MinValue, 9)]
        public void VarLongShouldRoundtrip(long i, int serializedLength)
        {
            var vi = new VarLong(i);
            var length = vi.GetSerializedLength();

            length.Should().Be(serializedLength);
            var builder = new BufferBuilder(length);
            var buffer = builder.AppendLong(vi).ToBuffer();
            var reader = new BufferReader(buffer);
            var i2 = reader.ReadLong();

            i.ToString("X16").Should().Be(i2.ToString("X16"));
            i.Should().Be(i2);
        }

        [Fact]
        public void SerializedLengthShouldBeCalculatedCorrectly()
        {
            //       64 leading zeros -> 1 byte varlong
            //    >= 57 leading zeros -> 1 byte varlong
            //    >= 50 leading zeros -> 2 byte varlong
            //    >= 43 leading zeros -> 3 byte varlong
            //    >= 36 leading zeros -> 4 byte varlong
            //    >= 29 leading zeros -> 5 byte varlong
            //    >= 22 leading zeros -> 6 byte varlong
            //    >= 15 leading zeros -> 7 byte varlong
            //    >= 8 leading zeros  -> 8 byte varlong
            //    else                -> 9 byte varlong
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000000).GetSerializedLength().Should().Be(1);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000001).GetSerializedLength().Should().Be(1);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000011).GetSerializedLength().Should().Be(1);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000111).GetSerializedLength().Should().Be(1);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_00011111).GetSerializedLength().Should().Be(1);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_00111111).GetSerializedLength().Should().Be(1);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_01111111).GetSerializedLength().Should().Be(1);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_11111111).GetSerializedLength().Should().Be(2);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00000000_00000001_11111111).GetSerializedLength().Should().Be(2);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00000000_00000011_11111111).GetSerializedLength().Should().Be(2);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00000000_00000111_11111111).GetSerializedLength().Should().Be(2);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00000000_00001111_11111111).GetSerializedLength().Should().Be(2);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00000000_00011111_11111111).GetSerializedLength().Should().Be(2);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00000000_00111111_11111111).GetSerializedLength().Should().Be(2);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00000000_01111111_11111111).GetSerializedLength().Should().Be(3);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00000000_11111111_11111111).GetSerializedLength().Should().Be(3);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00000001_11111111_11111111).GetSerializedLength().Should().Be(3);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00000011_11111111_11111111).GetSerializedLength().Should().Be(3);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00000111_11111111_11111111).GetSerializedLength().Should().Be(3);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00001111_11111111_11111111).GetSerializedLength().Should().Be(3);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00011111_11111111_11111111).GetSerializedLength().Should().Be(3);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_00111111_11111111_11111111).GetSerializedLength().Should().Be(4);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_01111111_11111111_11111111).GetSerializedLength().Should().Be(4);
            new VarLong(0b00000000_00000000_00000000_00000000_00000000_11111111_11111111_11111111).GetSerializedLength().Should().Be(4);
            new VarLong(0b00000000_00000000_00000000_00000000_00000001_11111111_11111111_11111111).GetSerializedLength().Should().Be(4);
            new VarLong(0b00000000_00000000_00000000_00000000_00000011_11111111_11111111_11111111).GetSerializedLength().Should().Be(4);
            new VarLong(0b00000000_00000000_00000000_00000000_00000111_11111111_11111111_11111111).GetSerializedLength().Should().Be(4);
            new VarLong(0b00000000_00000000_00000000_00000000_00001111_11111111_11111111_11111111).GetSerializedLength().Should().Be(4);
            new VarLong(0b00000000_00000000_00000000_00000000_00011111_11111111_11111111_11111111).GetSerializedLength().Should().Be(5);
            new VarLong(0b00000000_00000000_00000000_00000000_00111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(5);
            new VarLong(0b00000000_00000000_00000000_00000000_01111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(5);
            new VarLong(0b00000000_00000000_00000000_00000000_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(5);
            new VarLong(0b00000000_00000000_00000000_00000001_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(5);
            new VarLong(0b00000000_00000000_00000000_00000011_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(5);
            new VarLong(0b00000000_00000000_00000000_00000111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(5);
            new VarLong(0b00000000_00000000_00000000_00001111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(6);
            new VarLong(0b00000000_00000000_00000000_00011111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(6);
            new VarLong(0b00000000_00000000_00000000_00111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(6);
            new VarLong(0b00000000_00000000_00000000_01111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(6);
            new VarLong(0b00000000_00000000_00000000_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(6);
            new VarLong(0b00000000_00000000_00000001_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(6);
            new VarLong(0b00000000_00000000_00000011_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(6);
            new VarLong(0b00000000_00000000_00000111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(7);
            new VarLong(0b00000000_00000000_00001111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(7);
            new VarLong(0b00000000_00000000_00011111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(7);
            new VarLong(0b00000000_00000000_00111111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(7);
            new VarLong(0b00000000_00000000_01111111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(7);
            new VarLong(0b00000000_00000000_11111111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(7);
            new VarLong(0b00000000_00000001_11111111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(7);
            new VarLong(0b00000000_00000011_11111111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(8);
            new VarLong(0b00000000_00000111_11111111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(8);
            new VarLong(0b00000000_00001111_11111111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(8);
            new VarLong(0b00000000_00011111_11111111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(8);
            new VarLong(0b00000000_00111111_11111111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(8);
            new VarLong(0b00000000_01111111_11111111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(8);
            new VarLong(0b00000000_11111111_11111111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(8);
            new VarLong(0b00000001_11111111_11111111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(9);
            new VarLong(0b00000011_11111111_11111111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(9);
            new VarLong(0b00000111_11111111_11111111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(9);
            new VarLong(0b00001111_11111111_11111111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(9);
            new VarLong(0b00011111_11111111_11111111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(9);
            new VarLong(0b00111111_11111111_11111111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(9);
            new VarLong(0b01111111_11111111_11111111_11111111_11111111_11111111_11111111_11111111).GetSerializedLength().Should().Be(9);
            new VarLong(unchecked((long)0b11111111_11111111_11111111_11111111_11111111_11111111_11111111_11111111)).GetSerializedLength().Should().Be(9);
        }
    }
}
