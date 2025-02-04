using Wibblr.Grufs.Core;
using Wibblr.Grufs.Filesystem;
using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Tests
{
    public class DirectoryPathTests
    {
        [Fact]
        public void NullPathConstructorArgShouldThrow()
        {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            Should.Throw<ArgumentNullException>(() => new DirectoryPath((string?)null));
            Should.Throw<ArgumentNullException>(() => new DirectoryPath((BufferReader?)null));
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }

        [Fact]
        public void InvalidPathShouldThrow()
        {
            Should.Throw<ArgumentException>(() => new DirectoryPath("."));
            Should.Throw<ArgumentException>(() => new DirectoryPath(".."));
            Should.Throw<ArgumentException>(() => new DirectoryPath("./"));
            Should.Throw<ArgumentException>(() => new DirectoryPath("../"));
            Should.Throw<ArgumentException>(() => new DirectoryPath("/."));
            Should.Throw<ArgumentException>(() => new DirectoryPath("/.."));
            Should.Throw<ArgumentException>(() => new DirectoryPath("/./"));
            Should.Throw<ArgumentException>(() => new DirectoryPath("/../"));
        }

        [Theory]
        [InlineData("aBc/dEf")]
        public void PathWithValidCharactersShouldRoundtrip(string s)
        {
            var p = new DirectoryPath(s);

            var builder = new BufferBuilder(p.GetSerializedLength());
            var buffer = builder.AppendDirectoryPath(p).ToBuffer();

            var reader = new BufferReader(buffer);
            var p2 = reader.ReadDirectoryPath();

            p.ShouldBe(p2); // has implicit conversion to string
            p.ToString().ShouldBe(p2.ToString());
        }

        [Fact]
        public void FileMetadataShouldRoundtrip()
        {
            var name = new Filename("asdf");
            var address = new Address(new byte[32]);
            var vfsLastModified = new Timestamp("2002-01-01T10:00:00.1234567");
            var lastModified = new Timestamp("2001-01-01T10:00:00.1234567");
            var size = 123456L;
            
            var file = new VfsFileMetadata(name, address, 0, vfsLastModified, lastModified, size);

            var builder = new BufferBuilder(file.GetSerializedLength());
            var buffer = builder.AppendFileMetadata(file).ToBuffer();
            var reader = new BufferReader(buffer);

            var file2 = reader.ReadFileMetadata();

            file.ShouldBe(file2);
        }

        [Fact]
        public void VirtualDirectoryShouldRoundtrip()
        {
            var path = new DirectoryPath("a/b/c/d/e");
            var parentVersion = 123L;
            var vfsLastModified = new Timestamp("2002-01-01T10:00:00.1234567");
            var isDeleted = false;
            var size = 1234L;
            var files = new[]
            {
                new VfsFileMetadata(
                    new Filename("asdf"),
                    new Address(new byte[32]),
                    0,
                    vfsLastModified,
                    new Timestamp("2001-01-01T10:00:00.1234567"),
                    size),

                new VfsFileMetadata(
                    new Filename("qwer"),
                    new Address(new byte[32]),
                    0,
                    vfsLastModified,
                    new Timestamp("2001-01-01T10:00:00.1234567"),
                    size)
            };
            var directories = new[]
            {
                new Filename("f"),
                new Filename("g")
            };

            var directory = new VfsDirectoryMetadata(path, parentVersion, vfsLastModified, isDeleted, files, directories);
            
            var builder = new BufferBuilder(directory.GetSerializedLength());
            var buffer = builder.AppendVirtualDirectory(directory).ToBuffer();
            var reader = new BufferReader(buffer);

            var directory2 = reader.ReadVirtualDirectory();

            directory.ShouldBe(directory2);
        }


        [Fact]
        public void NormalizedPathShouldRemoveLeadingSlash()
        {
            var p = new DirectoryPath("/a/b/c");
            p.NormalizedPath.ShouldBe("a/b/c");
        }

        [Fact]
        public void NormalizedPathShouldRemoveTrailingSlash()
        {
            var p = new DirectoryPath("/a/b/c/");
            p.NormalizedPath.ShouldBe("a/b/c");
        }

        [Fact]
        public void NormalizedPathShouldRemoveDuplicateSlashOrBackslash()
        {
            var p = new DirectoryPath(@"//a\\b/\c/\");
            p.NormalizedPath.ShouldBe("a/b/c");
        }

        [Fact]
        public void CanonicalPathShouldBeLowercase()
        {
            var p = new DirectoryPath(@"/A\B/c\");
            p.CanonicalPath.ShouldBe("a/b/c");
        }

        [Fact]
        public void ParentPathShouldBeCalculated()
        {
            var p = new DirectoryPath(@"/");
            p.Parent().NormalizedPath.ShouldBe("");

            p = new DirectoryPath(@"/a\");
            p.Parent().NormalizedPath.ShouldBe("");

            p = new DirectoryPath(@"/a\b");
            p.Parent().NormalizedPath.ShouldBe("a");

            p = new DirectoryPath(@"/a\b/c\");
            p.Parent().NormalizedPath.ShouldBe("a/b");
        }

        [Fact]
        public void PathHierarchShouldBeCorrect()
        {
            var p = new DirectoryPath(@"");
            p.PathHierarchy().Count().ShouldBe(0);

            p = new DirectoryPath(@"/a");
            p.PathHierarchy().ShouldBe(new[]
            {
                (new DirectoryPath(""), new Filename("a"))
            });

            p = new DirectoryPath(@"/a\b/");
            p.PathHierarchy().ShouldBe(new[]
            {
                (new DirectoryPath(""), new Filename("a")),
                (new DirectoryPath("a"), new Filename("b")),
            });

            p = new DirectoryPath(@"/a\b/c\");
            p.PathHierarchy().ShouldBe(new[]
            {
                (new DirectoryPath(""), new Filename("a")),
                (new DirectoryPath("a"), new Filename("b")),
                (new DirectoryPath("a/b"), new Filename("c")),
            });
        }
    }
}
