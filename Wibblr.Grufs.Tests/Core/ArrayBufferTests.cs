using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Tests
{
    public class ArrayBufferTests
    {
        [Fact]
        public void ConstructorShouldThrowIfLengthIsGreaterThanCapacity()
        {
            Should.Throw<ArgumentException>(() => new ArrayBuffer(new byte[0], 1));
        }

        [Fact]
        public void ShouldThrowOnInvalidSpanLength()
        {
            var b = new ArrayBuffer(new byte[10], 5);

            b.AsSpan().Length.ShouldBe(5);
            Should.Throw<IndexOutOfRangeException>(() => b.AsSpan(0, 6));
        }

        [Fact]
        public void BuilderShouldThrowOnOverflow()
        {
            var b = new BufferBuilder(0);

            Should.Throw<IndexOutOfRangeException>(() => b.AppendByte(0));
            Should.Throw<IndexOutOfRangeException>(() => b.AppendInt(0));
            Should.Throw<IndexOutOfRangeException>(() => b.AppendLong(0));
        }

        [Fact]
        public void ReaderShouldThrowOnUnderflow()
        {
            var b = new ArrayBuffer(new byte[10], 1);
            var r = new BufferReader(b);

            ((int)r.ReadByte()).ShouldBe(0);
            Should.Throw<IndexOutOfRangeException>(() => r.ReadByte());
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

            ((int)reader.ReadByte()).ShouldBe(0x56);
            reader.ReadInt().ShouldBe(unchecked((int)0xCE12BD34));
            reader.ReadLong().ShouldBe(0x1234567890L);
            reader.ReadKnownLengthSpan(4).ToArray().ShouldBe([0x10, 0x20, 0x30, 0x40]);
            ((int)reader.ReadByte()).ShouldBe(0x67);
        }
    }

    public class LogTests
    {
        [Fact]
        public void NumbersShouldUseThousandsSeparators()
        {
            1.Format(false).ShouldBe("1");
            999.Format(false).ShouldBe("999");
            1000.Format(false).ShouldBe("1,000");
            1000000.Format(false).ShouldBe("1,000,000");

            1.Format(true).ShouldBe("1");
            999.Format(true).ShouldBe("999");
            1000.Format(true).ShouldBe("0.977 Ki");
            10000.Format(true).ShouldBe("9.77 Ki");
            100000.Format(true).ShouldBe("97.7 Ki");
            1000000.Format(true).ShouldBe("976.6 Ki");
            10000000.Format(true).ShouldBe("9.54 Mi");
            100000000.Format(true).ShouldBe("95.4 Mi");
            1000000000.Format(true).ShouldBe("953.7 Mi");
            10000000000.Format(true).ShouldBe("9.31 Gi");
            100000000000.Format(true).ShouldBe("93.1 Gi");
            1000000000000.Format(true).ShouldBe("931.3 Gi");
            10000000000000.Format(true).ShouldBe("9.09 Ti");
            100000000000000.Format(true).ShouldBe("90.9 Ti");
        }
    }
}
