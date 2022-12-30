using System;

using FluentAssertions;

using Xunit;

namespace Wibblr.Grufs.Tests
{
    public class RepositoryTest
    {
        [Fact]
        public void InitializeRepository()
        {
            var storage = new InMemoryChunkStorage();
            var r1 = new Repository(storage);
            r1.Initialize("hello");

            var r2 = new Repository(storage);
            r2.Open("hello");

            r1._masterKey.ToString().Should().Be(r2._masterKey.ToString());
        }

        [Fact]
        public void NormalizedPathShouldRemoveLeadingSlash()
        {
            var p = new RepositoryDirectoryPath("/a/b/c");
            p.NormalizedPath.Should().Be("a/b/c");
        }

        [Fact]
        public void NormalizedPathShouldRemoveTrailingSlash()
        {
            var p = new RepositoryDirectoryPath("/a/b/c/");
            p.NormalizedPath.Should().Be("a/b/c");
        }

        [Fact]
        public void NormalizedPathShouldRemoveDuplicateSlashOrBackslash()
        {
            var p = new RepositoryDirectoryPath(@"//a\\b/\c/\");
            p.NormalizedPath.Should().Be("a/b/c");
        }

        [Fact]
        public void CanonicalPathShouldBeLowercase()
        {
            var p = new RepositoryDirectoryPath(@"/A\B/c\");
            p.CanonicalPath.Should().Be("a/b/c");
        }

        [Fact]
        public void ParentPathShouldBeCalculated()
        {
            var p = new RepositoryDirectoryPath(@"/");
            p.Parent().NormalizedPath.Should().Be("");

            p = new RepositoryDirectoryPath(@"/a\");
            p.Parent().NormalizedPath.Should().Be("");

            p = new RepositoryDirectoryPath(@"/a\b");
            p.Parent().NormalizedPath.Should().Be("a");

            p = new RepositoryDirectoryPath(@"/a\b/c\");
            p.Parent().NormalizedPath.Should().Be("a/b");
        }

        [Fact]
        public void ParentAndNameShouldBeCorrect()
        {
            var p = new RepositoryDirectoryPath(@"");
            p.ParentAndName().Should().Be((new RepositoryDirectoryPath(""), new RepositoryFilename("")));

            p = new RepositoryDirectoryPath(@"/");
            p.ParentAndName().Should().Be((new RepositoryDirectoryPath(""), new RepositoryFilename("")));

            p = new RepositoryDirectoryPath(@"/a");
            p.ParentAndName().Should().Be((new RepositoryDirectoryPath(""), new RepositoryFilename("a")));

            p = new RepositoryDirectoryPath(@"/a\b/");
            p.ParentAndName().Should().Be((new RepositoryDirectoryPath("a"), new RepositoryFilename("b")));

            p = new RepositoryDirectoryPath(@"/a\b/c");
            p.ParentAndName().Should().Be((new RepositoryDirectoryPath("a/b"), new RepositoryFilename("c")));
        }

        [Fact]
        public void PathHierarchShouldBeCorrect()
        {
            var p = new RepositoryDirectoryPath(@"");
            p.PathHierarchy().Should().HaveCount(0);

            p = new RepositoryDirectoryPath(@"/a");
            p.PathHierarchy().Should().BeEquivalentTo(new[]
            {
                (new RepositoryDirectoryPath(""), new RepositoryFilename("a"))
            });

            p = new RepositoryDirectoryPath(@"/a\b/");
            p.PathHierarchy().Should().BeEquivalentTo(new[]
            {
                (new RepositoryDirectoryPath(""), new RepositoryFilename("a")),
                (new RepositoryDirectoryPath("a"), new RepositoryFilename("b")),
            });

            p = new RepositoryDirectoryPath(@"/a\b/c\");
            p.PathHierarchy().Should().BeEquivalentTo(new[]
            {
                (new RepositoryDirectoryPath(""), new RepositoryFilename("a")),
                (new RepositoryDirectoryPath("a"), new RepositoryFilename("b")),
                (new RepositoryDirectoryPath("a/b"), new RepositoryFilename("c")),
            });
        }
    }
}
