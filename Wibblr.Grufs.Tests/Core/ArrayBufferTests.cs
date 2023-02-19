using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Tests
{
    public class ArrayBufferTests
    {
        [Fact]
        public void ConstructorShouldThrowIfLengthIsGreaterThanCapacity()
        {
            new Action(() => new ArrayBuffer(new byte[0], 1)).Should().ThrowExactly<ArgumentException>();
        }

        [Fact]
        public void ShouldThrowOnInvalidSpanLength()
        {
            var b = new ArrayBuffer(new byte[10], 5);

            b.AsSpan().Length.Should().Be(5);
            new Action(() => b.AsSpan(0, 6)).Should().ThrowExactly<IndexOutOfRangeException>();
        }

        [Fact]
        public void BuilderShouldThrowOnOverflow()
        {
            var b = new BufferBuilder(0);

            new Action(() => b.AppendByte(0)).Should().ThrowExactly<IndexOutOfRangeException>();
            new Action(() => b.AppendInt(0)).Should().ThrowExactly<IndexOutOfRangeException>();
            new Action(() => b.AppendLong(0)).Should().ThrowExactly<IndexOutOfRangeException>();
        }

        [Fact]
        public void ReaderShouldThrowOnUnderflow()
        {
            var b = new ArrayBuffer(new byte[10], 1);
            var r = new BufferReader(b);

            r.ReadByte().Should().Be(0);
            new Action(() => r.ReadByte()).Should().ThrowExactly<IndexOutOfRangeException>();
        }

        [Fact]
        public void BufferShouldRoundtrip()
        {
            var b = new BufferBuilder(100)
                .AppendByte(0x56)
                .AppendInt(unchecked((int)0xCE12BD34))
                .AppendLong(0x1234567890L)
                .AppendBytes(new byte[] { 0x10, 0x20, 0x30, 0x40 })
                .AppendByte(0x67)
                .ToBuffer();

            Console.WriteLine(b);

            var reader = new BufferReader(b);

            reader.ReadByte().Should().Be(0x56);
            reader.ReadInt().Should().Be(unchecked((int)0xCE12BD34));
            reader.ReadLong().Should().Be(0x1234567890L);
            reader.ReadBytes(4).ToArray().Should().BeEquivalentTo(new byte[] { 0x10, 0x20, 0x30, 0x40 });
            reader.ReadByte().Should().Be(0x67);
        }
    }
}
