using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Tests.Core
{
    public class VarIntTests
    {
        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(127, 1)]
        [InlineData(128, 2)] /* 2^7 */
        [InlineData(129, 2)]
        [InlineData(16383, 2)]
        [InlineData(16384, 3)] /* 2^14 */
        [InlineData(16385, 3)]
        [InlineData(2097151, 3)]
        [InlineData(2097152, 4)] /* 2^21 */
        [InlineData(2097153, 4)]
        [InlineData(268435455, 4)]
        [InlineData(268435456, 5)] /* 2^28 */
        [InlineData(268435457, 5)]
        [InlineData(int.MaxValue, 5)]
        [InlineData(-1, 5)]
        public void VarIntShouldRoundtrip(int i, int serializedLength)
        {
            var vi = new VarInt(i);
            var length = vi.GetSerializedLength();

            length.ShouldBe(serializedLength);
            var builder = new BufferBuilder(length);
            var buffer = builder.AppendInt(i).ToBuffer();
            var reader = new BufferReader(buffer);
            var i2 = reader.ReadInt();

            i.ShouldBe(i2);
        }

        [Fact]
        public void InvalidVarIntShouldThrow()
        {
            var builder = new BufferBuilder(10);
            var buffer = builder.AppendByte(0xFF).ToBuffer();
            var reader = new BufferReader(buffer);

            Should.Throw<Exception>(() => reader.ReadInt());
        }

        [Fact]
        public void SerializedLengthShouldBeCalculatedCorrectly()
        {
            //       32 leading zeros -> 1 byte varint
            //    >= 25 leading zeros -> 1 byte varint
            //    >= 18 leading zeros -> 2 byte varint
            //    >= 11 leading zeros -> 3 byte varint
            //    >= 4  leading zeros -> 4 byte varint
            //    else                -> 5 byte varint
            new VarInt(0b00000000_00000000_00000000_00000000).GetSerializedLength().ShouldBe(1);
            new VarInt(0b00000000_00000000_00000000_00000001).GetSerializedLength().ShouldBe(1);
            new VarInt(0b00000000_00000000_00000000_00000011).GetSerializedLength().ShouldBe(1);
            new VarInt(0b00000000_00000000_00000000_00000111).GetSerializedLength().ShouldBe(1);
            new VarInt(0b00000000_00000000_00000000_00011111).GetSerializedLength().ShouldBe(1);
            new VarInt(0b00000000_00000000_00000000_00111111).GetSerializedLength().ShouldBe(1);
            new VarInt(0b00000000_00000000_00000000_01111111).GetSerializedLength().ShouldBe(1);
            new VarInt(0b00000000_00000000_00000000_11111111).GetSerializedLength().ShouldBe(2);
            new VarInt(0b00000000_00000000_00000001_11111111).GetSerializedLength().ShouldBe(2);
            new VarInt(0b00000000_00000000_00000011_11111111).GetSerializedLength().ShouldBe(2);
            new VarInt(0b00000000_00000000_00000111_11111111).GetSerializedLength().ShouldBe(2);
            new VarInt(0b00000000_00000000_00001111_11111111).GetSerializedLength().ShouldBe(2);
            new VarInt(0b00000000_00000000_00011111_11111111).GetSerializedLength().ShouldBe(2);
            new VarInt(0b00000000_00000000_00111111_11111111).GetSerializedLength().ShouldBe(2);
            new VarInt(0b00000000_00000000_01111111_11111111).GetSerializedLength().ShouldBe(3);
            new VarInt(0b00000000_00000000_11111111_11111111).GetSerializedLength().ShouldBe(3);
            new VarInt(0b00000000_00000001_11111111_11111111).GetSerializedLength().ShouldBe(3);
            new VarInt(0b00000000_00000011_11111111_11111111).GetSerializedLength().ShouldBe(3);
            new VarInt(0b00000000_00000111_11111111_11111111).GetSerializedLength().ShouldBe(3);
            new VarInt(0b00000000_00001111_11111111_11111111).GetSerializedLength().ShouldBe(3);
            new VarInt(0b00000000_00011111_11111111_11111111).GetSerializedLength().ShouldBe(3);
            new VarInt(0b00000000_00111111_11111111_11111111).GetSerializedLength().ShouldBe(4);
            new VarInt(0b00000000_01111111_11111111_11111111).GetSerializedLength().ShouldBe(4);
            new VarInt(0b00000000_11111111_11111111_11111111).GetSerializedLength().ShouldBe(4);
            new VarInt(0b00000001_11111111_11111111_11111111).GetSerializedLength().ShouldBe(4);
            new VarInt(0b00000011_11111111_11111111_11111111).GetSerializedLength().ShouldBe(4);
            new VarInt(0b00000111_11111111_11111111_11111111).GetSerializedLength().ShouldBe(4);
            new VarInt(0b00001111_11111111_11111111_11111111).GetSerializedLength().ShouldBe(4);
            new VarInt(0b00011111_11111111_11111111_11111111).GetSerializedLength().ShouldBe(5);
            new VarInt(0b00111111_11111111_11111111_11111111).GetSerializedLength().ShouldBe(5);
            new VarInt(0b01111111_11111111_11111111_11111111).GetSerializedLength().ShouldBe(5);
            new VarInt(unchecked((int)0b11111111_11111111_11111111_11111111)).GetSerializedLength().ShouldBe(5);
        }
    }
}
