using Wibblr.Grufs.Core;
using Wibblr.Grufs.Filesystem;

namespace Wibblr.Grufs.Tests
{
    public class RepositoryTest
    {
        [Fact]
        public void RepositoryInitAndOpen()
        {
            var storage = new InMemoryChunkStorage();
            var r1 = new Repository("myrepo", storage, "hello");
            r1.Initialize();

            var r2 = new Repository("myrepo", storage, "hello");
            r2.Open();
            r1.MasterKey.ToString().Should().Be(r2.MasterKey.ToString());
        }

        [Fact]
        public void NormalizedPathShouldRemoveLeadingSlash()
        {
            var p = new DirectoryPath("/a/b/c");
            p.NormalizedPath.Should().Be("a/b/c");
        }

        [Fact]
        public void NormalizedPathShouldRemoveTrailingSlash()
        {
            var p = new DirectoryPath("/a/b/c/");
            p.NormalizedPath.Should().Be("a/b/c");
        }

        [Fact]
        public void NormalizedPathShouldRemoveDuplicateSlashOrBackslash()
        {
            var p = new DirectoryPath(@"//a\\b/\c/\");
            p.NormalizedPath.Should().Be("a/b/c");
        }

        [Fact]
        public void CanonicalPathShouldBeLowercase()
        {
            var p = new DirectoryPath(@"/A\B/c\");
            p.CanonicalPath.Should().Be("a/b/c");
        }

        [Fact]
        public void ParentPathShouldBeCalculated()
        {
            var p = new DirectoryPath(@"/");
            p.Parent().NormalizedPath.Should().Be("");

            p = new DirectoryPath(@"/a\");
            p.Parent().NormalizedPath.Should().Be("");

            p = new DirectoryPath(@"/a\b");
            p.Parent().NormalizedPath.Should().Be("a");

            p = new DirectoryPath(@"/a\b/c\");
            p.Parent().NormalizedPath.Should().Be("a/b");
        }

        [Fact]
        public void PathHierarchShouldBeCorrect()
        {
            var p = new DirectoryPath(@"");
            p.PathHierarchy().Should().HaveCount(0);

            p = new DirectoryPath(@"/a");
            p.PathHierarchy().Should().BeEquivalentTo(new[]
            {
                (new DirectoryPath(""), new Filename("a"))
            });

            p = new DirectoryPath(@"/a\b/");
            p.PathHierarchy().Should().BeEquivalentTo(new[]
            {
                (new DirectoryPath(""), new Filename("a")),
                (new DirectoryPath("a"), new Filename("b")),
            });

            p = new DirectoryPath(@"/a\b/c\");
            p.PathHierarchy().Should().BeEquivalentTo(new[]
            {
                (new DirectoryPath(""), new Filename("a")),
                (new DirectoryPath("a"), new Filename("b")),
                (new DirectoryPath("a/b"), new Filename("c")),
            });
        }

        // Test filename translation
    }
}
