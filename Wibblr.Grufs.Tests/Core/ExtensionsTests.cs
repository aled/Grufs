using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Tests.Core
{
    public class ExtensionsTests
    {
        [Fact]
        public void SplitLast()
        {
            "".SplitLast('/').ShouldBe(("", ""));
            "/".SplitLast('/').ShouldBe(("", ""));
            "/a".SplitLast('/').ShouldBe(("", "a"));
            "a/".SplitLast('/').ShouldBe(("a", ""));
            "a/b".SplitLast('/').ShouldBe(("a", "b"));
            "/a/b".SplitLast('/').ShouldBe(("/a", "b"));
            "/a/b/c".SplitLast('/').ShouldBe(("/a/b", "c"));
        }
    }
}
