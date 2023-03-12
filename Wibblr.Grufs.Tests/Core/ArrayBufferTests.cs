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
                .AppendKnownLengthSpan(new byte[] { 0x10, 0x20, 0x30, 0x40 })
                .AppendByte(0x67)
                .ToBuffer();

            Log.WriteLine(0, b.ToString());

            var reader = new BufferReader(b);

            reader.ReadByte().Should().Be(0x56);
            reader.ReadInt().Should().Be(unchecked((int)0xCE12BD34));
            reader.ReadLong().Should().Be(0x1234567890L);
            reader.ReadKnownLengthSpan(4).ToArray().Should().BeEquivalentTo(new byte[] { 0x10, 0x20, 0x30, 0x40 });
            reader.ReadByte().Should().Be(0x67);
        }
    }

    public class LogTests
    {
        [Fact]
        public void NumbersShouldUseThousandsSeparators()
        {
            1.Format(false).Should().Be("1");
            999.Format(false).Should().Be("999");
            1000.Format(false).Should().Be("1,000");
            1000000.Format(false).Should().Be("1,000,000");

            1.Format(true).Should().Be("1");
            999.Format(true).Should().Be("999");
            1000.Format(true).Should().Be("0.977 Ki");
            10000.Format(true).Should().Be("9.77 Ki");
            100000.Format(true).Should().Be("97.7 Ki");
            1000000.Format(true).Should().Be("976.6 Ki");
            10000000.Format(true).Should().Be("9.54 Mi");
            100000000.Format(true).Should().Be("95.4 Mi");
            1000000000.Format(true).Should().Be("953.7 Mi");
            10000000000.Format(true).Should().Be("9.31 Gi");
            100000000000.Format(true).Should().Be("93.1 Gi");
            1000000000000.Format(true).Should().Be("931.3 Gi");
            10000000000000.Format(true).Should().Be("9.09 Ti");
            100000000000000.Format(true).Should().Be("90.9 Ti");
        }
    }
}
