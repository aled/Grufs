using System;

using FluentAssertions;

namespace Wibblr.Grufs.Tests
{
    public class SerializationTests
    {
        [Fact]
        public void InvalidFilenamesShouldThrow()
        {
            new Action(() => new RepositoryFilename("/")).Should().ThrowExactly<ArgumentException>();
            new Action(() => new RepositoryFilename(".")).Should().ThrowExactly<ArgumentException>();
            new Action(() => new RepositoryFilename("..")).Should().ThrowExactly<ArgumentException>();
        }

        [Fact]
        public void NullFilenameConstructorArgShouldThrow()
        {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            new Action(() => new RepositoryFilename((string?)null)).Should().ThrowExactly<ArgumentNullException>();
            new Action(() => new RepositoryFilename((BufferReader?)null)).Should().ThrowExactly<ArgumentNullException>();
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }

        [Fact]
        public void FilenameShouldCanonicalize()
        {
            var name = new RepositoryFilename("ASDF");

            name.ToString().Should().Be("ASDF");
            name.OriginalName.Should().Be("ASDF");
            name.CanonicalName.Should().Be("asdf");
            name.GetSerializedLength().Should().Be(18); // length = 1 content = 2 * 4, so 9 for each of original and canonical

            var builder = new BufferBuilder(name.GetSerializedLength());
            var buffer = builder.AppendRepositoryFilename(name).ToBuffer();
            buffer.AsSpan().ToArray().Should().BeEquivalentTo(new byte[] { 4, (byte)'A', 0, (byte)'S', 0, (byte)'D', 0, (byte)'F', 0, 4, (byte)'a', 0, (byte)'s', 0, (byte)'d', 0, (byte)'f', 0 });

            var reader = new BufferReader(buffer);
            var name2 = reader.ReadRepositoryFilename();
            name2.Should().Be(name);
        }

        [Theory]
        [InlineData("aBc")]
        public void FilenameWithValidCharactersShouldRoundtrip(string s)
        {
            var ps = new RepositoryFilename(s);

            var builder = new BufferBuilder(ps.GetSerializedLength());
            var buffer = builder.AppendRepositoryFilename(ps).ToBuffer();

            var reader = new BufferReader(buffer);
            var s2 = reader.ReadRepositoryFilename();

            ps.ToString().Should().Be(s2.ToString());
        }

        [Fact]
        public void NullPathConstructorArgShouldThrow()
        {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            new Action(() => new RepositoryDirectoryPath((string?)null)).Should().ThrowExactly<ArgumentNullException>();
            new Action(() => new RepositoryDirectoryPath((BufferReader?)null)).Should().ThrowExactly<ArgumentNullException>();
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }

        [Fact]
        public void InvalidPathShouldThrow()
        {
            new Action(() => new RepositoryDirectoryPath(".")).Should().ThrowExactly<ArgumentException>();
            new Action(() => new RepositoryDirectoryPath("..")).Should().ThrowExactly<ArgumentException>();
            new Action(() => new RepositoryDirectoryPath("./")).Should().ThrowExactly<ArgumentException>();
            new Action(() => new RepositoryDirectoryPath("../")).Should().ThrowExactly<ArgumentException>();
            new Action(() => new RepositoryDirectoryPath("/.")).Should().ThrowExactly<ArgumentException>();
            new Action(() => new RepositoryDirectoryPath("/..")).Should().ThrowExactly<ArgumentException>();
            new Action(() => new RepositoryDirectoryPath("/./")).Should().ThrowExactly<ArgumentException>();
            new Action(() => new RepositoryDirectoryPath("/../")).Should().ThrowExactly<ArgumentException>();
        }

        [Theory]
        [InlineData("aBc/dEf")]
        public void PathWithValidCharactersShouldRoundtrip(string s)
        {
            var p = new RepositoryDirectoryPath(s);

            var builder = new BufferBuilder(p.GetSerializedLength());
            var buffer = builder.AppendRepositoryDirectoryPath(p).ToBuffer();

            var reader = new BufferReader(buffer);
            var p2 = reader.ReadRepositoryDirectoryPath();

            p.Should().Be(p2); // has implicit conversion to string
            p.ToString().Should().Be(p2.ToString());
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(127, 1)]
        [InlineData(128, 2)] /* 2^7 */
        [InlineData(129, 2)]
        [InlineData(16383, 2)]
        [InlineData(16384, 3)] /* 2^14 */
        [InlineData(16385, 3)]
        [InlineData(2097151, 3)]
        [InlineData(2097152, 4)] /* 2^21 */
        [InlineData(2097153, 4)]
        [InlineData(268435455, 4)]
        [InlineData(268435456, 5)] /* 2^28 */
        [InlineData(268435457, 5)]
        [InlineData(int.MaxValue, 5)]
        [InlineData(-1, 5)]
        public void VarIntShouldRoundtrip(int i, int serializedLength)
        {
            var vi = new VarInt(i);
            var length = vi.GetSerializedLength();

            length.Should().Be(serializedLength);
            var builder = new BufferBuilder(length);
            var buffer = builder.AppendVarInt(vi).ToBuffer();
            var reader = new BufferReader(buffer);
            var vi2 = reader.ReadVarInt();

            vi.Should().Be(vi2);
        }

        [Fact]
        public void InvalidVarIntShouldThrow()
        {
            var builder = new BufferBuilder(10);
            var buffer = builder.AppendByte(0xFF).ToBuffer(); 
            var reader = new BufferReader(buffer);

            new Action(() => reader.ReadVarInt()).Should().Throw<Exception>();
        }

        [Fact]
        public void RepositoryFileShouldRoundtrip()
        {
            var name = new RepositoryFilename("asdf");
            var address = new Address(new byte[32]);
            var timestamp = new Timestamp("2001-01-01T10:00:00.1234567");

            var file = new RepositoryFile(name, address, ChunkType.Content, timestamp);

            var builder = new BufferBuilder(file.GetSerializedLength());
            var buffer = builder.AppendRepositoryFile(file).ToBuffer();
            var reader = new BufferReader(buffer);

            var file2 = reader.ReadRepositoryFile();

            file.Should().Be(file2);
        }

        [Fact]
        public void RepositoryDirectoryShouldRoundtrip()
        {
            var path = new RepositoryDirectoryPath("a/b/c/d/e");
            var parentVersion = 123L;
            var timestamp = new Timestamp("2001-01-01T10:00:00.1234567");
            var isDeleted = false;
            var files = new[]
            {
                new RepositoryFile(
                    new RepositoryFilename("asdf"),
                    new Address(new byte[32]),
                    ChunkType.Content,
                    new Timestamp("2001-01-01T10:00:00.1234567")),

                new RepositoryFile(
                    new RepositoryFilename("qwer"),
                    new Address(new byte[32]),
                    ChunkType.Content,
                    new Timestamp("2001-01-01T10:00:00.1234567"))
            };
            var directories = new[]
            {
                new RepositoryFilename("f"),
                new RepositoryFilename("g")
            };

            var directory = new RepositoryDirectory(path, parentVersion, timestamp, isDeleted, files, directories);
            
            var builder = new BufferBuilder(directory.GetSerializedLength());
            var buffer = builder.AppendRepositoryDirectory(directory).ToBuffer();
            var reader = new BufferReader(buffer);

            var directory2 = reader.ReadRepositoryDirectory();

            directory.Should().Be(directory2);
        }
    }
}
