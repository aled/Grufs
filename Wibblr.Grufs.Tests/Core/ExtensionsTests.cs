using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Tests.Core
{
    public class ExtensionsTests
    {
        [Fact]
        public void SplitLast()
        {
            "".SplitLast('/').Should().Be(("", ""));
            "/".SplitLast('/').Should().Be(("", ""));
            "/a".SplitLast('/').Should().Be(("", "a"));
            "a/".SplitLast('/').Should().Be(("a", ""));
            "a/b".SplitLast('/').Should().Be(("a", "b"));
            "/a/b".SplitLast('/').Should().Be(("/a", "b"));
            "/a/b/c".SplitLast('/').Should().Be(("/a/b", "c"));
        }
    }
}
