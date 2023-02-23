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
    }
}
