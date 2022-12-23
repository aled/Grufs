using System;

using FluentAssertions;

namespace Wibblr.Grufs.Tests
{
    public class SerializationTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("a")]
        [InlineData("abc")]
        [InlineData("abc/d/e")]
        public void Utf8StringWithValidCharactersShouldRoundtrip(string s)
        {
            var ps = new PathString(s);

            var builder = new BufferBuilder(ps.GetSerializedLength());
            var buffer = builder.AppendPathString(ps).ToBuffer();

            var reader = new BufferReader(buffer);
            var s2 = reader.ReadPathString();

            ps.ToString().Should().Be(s2.ToString());
        }

        [Theory]
        [InlineData(":", "%3A")]
        [InlineData("a?", "a%3F")]
        [InlineData("abc\\", "abc/")]
        [InlineData("abc/d\\e", "abc/d/e")]
        public void Utf8StringWithInvalidCharactersShouldRoundtrip(string s, string sanitized)
        {
            var ps = new PathString(s);

            var builder = new BufferBuilder(ps.GetSerializedLength());
            var buffer = builder.AppendPathString(ps).ToBuffer();

            var reader = new BufferReader(buffer);
            var s2 = reader.ReadPathString();

            ps.ToString().Should().Be(sanitized);
        }

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
            var buffer = builder.AppendVarInt(vi).ToBuffer();
            var reader = new BufferReader(buffer);
            var vi2 = reader.ReadVarInt();

            vi.Should().Be(vi2);
        }

        [Fact]
        public void InvalidVarIntShouldThrow()
        {
            var builder = new BufferBuilder(10);
            var buffer = builder.AppendByte(0xFF).ToBuffer(); 
            var reader = new BufferReader(buffer);

            new Action(() => reader.ReadVarInt()).Should().Throw<Exception>();
        }
    }
}
