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

            length.Should().Be(serializedLength);
            var builder = new BufferBuilder(length);
            var buffer = builder.AppendInt(i).ToBuffer();
            var reader = new BufferReader(buffer);
            var i2 = reader.ReadInt();

            i.Should().Be(i2);
        }

        [Fact]
        public void InvalidVarIntShouldThrow()
        {
            var builder = new BufferBuilder(10);
            var buffer = builder.AppendByte(0xFF).ToBuffer();
            var reader = new BufferReader(buffer);

            new Action(() => reader.ReadInt()).Should().Throw<Exception>();
        }
    }
}
