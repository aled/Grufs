using Wibblr.Grufs.Core;

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

            serialized.ShouldBe("a:\"a\"\n");

            var deserialized = new SkvSerializer().Deserialize(serialized);
            deserialized.ShouldBeEquivalentTo(data);
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

            serialized.ShouldBe("a:" + LiteralDoubleQuote + "a" + EscapedDoubleQuote + EscapedBackslash + LiteralDoubleQuote + "\n");

            var deserialized = new SkvSerializer().Deserialize(serialized);
            deserialized.ShouldBeEquivalentTo(data);
        }

        [Fact]
        public void CanSerializeStringWithSpecialChars()
        {
            var data = new List<KeyValuePair<string, object>>();
            data.Add(new KeyValuePair<string, object>("a", "a\r\n\t"));

            var serialized = new SkvSerializer().Serialize(data);

            serialized.ShouldBe("a:\"a\\r\\n\\t\"" + "\n");

            var deserialized = new SkvSerializer().Deserialize(serialized);
            deserialized.ShouldBeEquivalentTo(data);
        }

        [Fact]
        public void CanSerializeStringWithControlChars()
        {
            var data = new List<KeyValuePair<string, object>>();
            data.Add(new KeyValuePair<string, object>("a", "a\x00\x01"));

            var serialized = new SkvSerializer().Serialize(data);

            serialized.ShouldBe("a:\"a\\x00\\x01\"" + "\n");

            var deserialized = new SkvSerializer().Deserialize(serialized);
            deserialized.ShouldBeEquivalentTo(data);
        }

        [Fact]
        public void CanSerializeStringWithUnicodeChars()
        {
            var data = new List<KeyValuePair<string, object>>();
            data.Add(new KeyValuePair<string, object>("a", "a\u1234\u5678"));

            var serialized = new SkvSerializer().Serialize(data);

            serialized.ShouldBe("a:\"a\\u1234\\u5678\"" + "\n");

            var deserialized = new SkvSerializer().Deserialize(serialized);
            deserialized.ShouldBeEquivalentTo(data);
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

            serialized.ShouldBe(
                  "a:2000-01-01T00:00:00.0000000" + "\n" 
                + "a:true" + "\n" 
                + "a:false" + "\n"
                + "a:null" + "\n"
                + "a:0x010203" + "\n"
                + "a:123" + "\n"
            );

            var deserialized = new SkvSerializer().Deserialize(serialized);

            deserialized.ToString().ShouldBe(data.ToString());
        }
    }
}
