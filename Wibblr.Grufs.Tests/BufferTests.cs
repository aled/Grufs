using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FluentAssertions;

using Xunit;

namespace Wibblr.Grufs.Tests
{
    public class BufferTests
    {
        [Fact]
        public void BufferShouldOverflow()
        {
            var b = new BufferBuilder(0);

            new Action(() => b.AppendByte((byte)0)).Should().ThrowExactly<IndexOutOfRangeException>();
            new Action(() => b.AppendInt(0)).Should().ThrowExactly<IndexOutOfRangeException>();
            new Action(() => b.AppendLong(0L)).Should().ThrowExactly<IndexOutOfRangeException>();
            new Action(() => b.AppendByte((byte)0)).Should().ThrowExactly<IndexOutOfRangeException>();
            new Action(() => b.AppendByte((byte)0)).Should().ThrowExactly<IndexOutOfRangeException>();
        }

        [Fact]
        public void BufferShouldRoundtrip()
        {
            var bytes = new byte[100];
            BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(23), 0x12345678901234);
            var x = BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(23, sizeof(long)));
            Console.WriteLine(Convert.ToHexString(bytes.AsSpan(23)));

            x.Should().Be(0x12345678901234);

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
