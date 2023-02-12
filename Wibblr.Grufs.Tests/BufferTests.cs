using FluentAssertions;

using Wibblr.Grufs.Core;

using Buffer = Wibblr.Grufs.Core.ArrayBuffer;

namespace Wibblr.Grufs.Tests
{
    public class SkvSerializerTests
    {
        [Fact]
        public void CanSerializeString()
        {
            var data = new List<KeyValuePair<string, object>> ();
            data.Add(new KeyValuePair<string, object>("a", "a"));

            var serialized = new SkvSerializer().Serialize(data);

            serialized.Should().Be("a:\"a\"\n");

            var deserialized = new SkvSerializer().Deserialize(serialized);
            deserialized.Should().BeEquivalentTo(data);
        }

        static char LiteralDoubleQuote = '\"';
        static char LiteralBackslash = '\\';
        static string EscapedDoubleQuote = $"{LiteralBackslash}{LiteralDoubleQuote}";
        static string EscapedBackslash = $"{LiteralBackslash}{LiteralBackslash}";

        [Fact]
        public void CanSerializeStringWithDoubleQuote()
        {
            var data = new List<KeyValuePair<string, object>>();
            data.Add(new KeyValuePair<string, object>("a", "a" + LiteralDoubleQuote + LiteralBackslash));

            var serialized = new SkvSerializer().Serialize(data);

            serialized.Should().Be("a:" + LiteralDoubleQuote + "a" + EscapedDoubleQuote + EscapedBackslash + LiteralDoubleQuote + "\n");

            var deserialized = new SkvSerializer().Deserialize(serialized);
            deserialized.Should().BeEquivalentTo(data);
        }

        [Fact]
        public void CanSerializeStringWithSpecialChars()
        {
            var data = new List<KeyValuePair<string, object>>();
            data.Add(new KeyValuePair<string, object>("a", "a\r\n\t"));

            var serialized = new SkvSerializer().Serialize(data);

            serialized.Should().Be("a:\"a\\r\\n\\t\"" + "\n");

            var deserialized = new SkvSerializer().Deserialize(serialized);
            deserialized.Should().BeEquivalentTo(data);
        }

        [Fact]
        public void CanSerializeStringWithControlChars()
        {
            var data = new List<KeyValuePair<string, object>>();
            data.Add(new KeyValuePair<string, object>("a", "a\x00\x01"));

            var serialized = new SkvSerializer().Serialize(data);

            serialized.Should().Be("a:\"a\\x00\\x01\"" + "\n");

            var deserialized = new SkvSerializer().Deserialize(serialized);
            deserialized.Should().BeEquivalentTo(data);
        }

        [Fact]
        public void CanSerializeStringWithUnicodeChars()
        {
            var data = new List<KeyValuePair<string, object>>();
            data.Add(new KeyValuePair<string, object>("a", "a\u1234\u5678"));

            var serialized = new SkvSerializer().Serialize(data);

            serialized.Should().Be("a:\"a\\u1234\\u5678\"" + "\n");

            var deserialized = new SkvSerializer().Deserialize(serialized);
            deserialized.Should().BeEquivalentTo(data);
        }

        [Fact]
        public void CanSerializeTypes()
        {

            var data = new List<KeyValuePair<string, object>>();
            data.Add(new KeyValuePair<string, object>("a", DateTime.Parse("2000-01-01")));
            data.Add(new KeyValuePair<string, object>("a", true));
            data.Add(new KeyValuePair<string, object>("a", false));
            data.Add(new KeyValuePair<string, object>("a", null!));
            data.Add(new KeyValuePair<string, object>("a", new byte[] {1, 2, 3}));
            data.Add(new KeyValuePair<string, object>("a", 123));

            var serialized = new SkvSerializer().Serialize(data);

            serialized.Should().Be(
                  "a:2000-01-01T00:00:00.0000000" + "\n" 
                + "a:true" + "\n" 
                + "a:false" + "\n"
                + "a:null" + "\n"
                + "a:0x010203" + "\n"
                + "a:123" + "\n"
            );

            var deserialized = new SkvSerializer().Deserialize(serialized);
            deserialized.Should().BeEquivalentTo(data);
        }
    }

    public class BufferTests
    {
        [Fact]
        public void ConstructorShouldThrowIfLengthIsGreaterThanCapacity()
        {
            new Action(() => new Buffer(new byte[0], 1)).Should().ThrowExactly<ArgumentException>();
        }

        [Fact]
        public void ShouldThrowOnInvalidSpanLength()
        {
            var b = new Buffer(new byte[10], 5);

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
            var b = new Buffer(new byte[10], 1);
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
