using System;

using Wibblr.Grufs;

using FluentAssertions;

namespace Wibblr.Grufs.Tests
{
    public class SerializationTests
    {
        [Fact]
        public void InvalidFilenamesShouldThrow()
        {
            new Action(() => new Filename("/")).Should().ThrowExactly<ArgumentException>();
            new Action(() => new Filename(".")).Should().ThrowExactly<ArgumentException>();
            new Action(() => new Filename("..")).Should().ThrowExactly<ArgumentException>();
        }

        [Fact]
        public void NullFilenameConstructorArgShouldThrow()
        {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            new Action(() => new Filename((string?)null)).Should().ThrowExactly<ArgumentNullException>();
            new Action(() => new Filename((BufferReader?)null)).Should().ThrowExactly<ArgumentNullException>();
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }

        [Fact]
        public void FilenameShouldCanonicalize()
        {
            var name = new Filename("ASDF");

            name.ToString().Should().Be("ASDF");
            name.OriginalName.Should().Be("ASDF");
            name.CanonicalName.Should().Be("asdf");
            name.GetSerializedLength().Should().Be(18); // length = 1 content = 2 * 4, so 9 for each of original and canonical

            var builder = new BufferBuilder(name.GetSerializedLength());
            var buffer = builder.AppendFilename(name).ToBuffer();
            buffer.AsSpan().ToArray().Should().BeEquivalentTo(new byte[] { 4, (byte)'A', 0, (byte)'S', 0, (byte)'D', 0, (byte)'F', 0, 4, (byte)'a', 0, (byte)'s', 0, (byte)'d', 0, (byte)'f', 0 });

            var reader = new BufferReader(buffer);
            var name2 = reader.ReadFilename();
            name2.Should().Be(name);
        }

        [Theory]
        [InlineData("aBc")]
        public void FilenameWithValidCharactersShouldRoundtrip(string s)
        {
            var ps = new Filename(s);

            var builder = new BufferBuilder(ps.GetSerializedLength());
            var buffer = builder.AppendFilename(ps).ToBuffer();

            var reader = new BufferReader(buffer);
            var s2 = reader.ReadFilename();

            ps.ToString().Should().Be(s2.ToString());
        }

        [Fact]
        public void NullPathConstructorArgShouldThrow()
        {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            new Action(() => new DirectoryPath((string?)null)).Should().ThrowExactly<ArgumentNullException>();
            new Action(() => new DirectoryPath((BufferReader?)null)).Should().ThrowExactly<ArgumentNullException>();
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
        }

        [Fact]
        public void InvalidPathShouldThrow()
        {
            new Action(() => new DirectoryPath(".")).Should().ThrowExactly<ArgumentException>();
            new Action(() => new DirectoryPath("..")).Should().ThrowExactly<ArgumentException>();
            new Action(() => new DirectoryPath("./")).Should().ThrowExactly<ArgumentException>();
            new Action(() => new DirectoryPath("../")).Should().ThrowExactly<ArgumentException>();
            new Action(() => new DirectoryPath("/.")).Should().ThrowExactly<ArgumentException>();
            new Action(() => new DirectoryPath("/..")).Should().ThrowExactly<ArgumentException>();
            new Action(() => new DirectoryPath("/./")).Should().ThrowExactly<ArgumentException>();
            new Action(() => new DirectoryPath("/../")).Should().ThrowExactly<ArgumentException>();
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
        public void FileMetadataShouldRoundtrip()
        {
            var name = new Filename("asdf");
            var address = new Address(new byte[32]);
            var timestamp = new Timestamp("2001-01-01T10:00:00.1234567");

            var file = new FileMetadata(name, address, 0, timestamp);

            var builder = new BufferBuilder(file.GetSerializedLength());
            var buffer = builder.AppendFileMetadata(file).ToBuffer();
            var reader = new BufferReader(buffer);

            var file2 = reader.ReadFileMetadata();

            file.Should().Be(file2);
        }

        [Fact]
        public void VirtualDirectoryShouldRoundtrip()
        {
            var path = new DirectoryPath("a/b/c/d/e");
            var parentVersion = 123L;
            var timestamp = new Timestamp("2001-01-01T10:00:00.1234567");
            var isDeleted = false;
            var files = new[]
            {
                new FileMetadata(
                    new Filename("asdf"),
                    new Address(new byte[32]),
                    0,
                    new Timestamp("2001-01-01T10:00:00.1234567")),

                new FileMetadata(
                    new Filename("qwer"),
                    new Address(new byte[32]),
                    0,
                    new Timestamp("2001-01-01T10:00:00.1234567"))
            };
            var directories = new[]
            {
                new Filename("f"),
                new Filename("g")
            };

            var directory = new VirtualDirectory(path, parentVersion, timestamp, isDeleted, files, directories);
            
            var builder = new BufferBuilder(directory.GetSerializedLength());
            var buffer = builder.AppendVirtualDirectory(directory).ToBuffer();
            var reader = new BufferReader(buffer);

            var directory2 = reader.ReadVirtualDirectory();

            directory.Should().Be(directory2);
        }
    }
}
