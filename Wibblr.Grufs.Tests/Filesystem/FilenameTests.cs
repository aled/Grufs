using Wibblr.Grufs.Core;
using Wibblr.Grufs.Filesystem;

namespace Wibblr.Grufs.Tests
{
    public class FilenameTests
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
    }
}
