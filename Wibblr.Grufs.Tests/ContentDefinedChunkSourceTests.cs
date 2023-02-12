using System.Text;

using FluentAssertions;

using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Tests
{
    public class ContentDefinedChunkSourceTests
    {
        [Fact]
        public void ShouldSplitAtPreviouslyCalculatedLocations()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 5000; i++)
                sb.Append($"{i} - The quick brown fox jumps over the lazy dog - {i}...");

            var bytes = Encoding.ASCII.GetBytes(sb.ToString());
            var stream = new MemoryStream(bytes);
            var byteSource = new StreamByteSource(stream);
            var chunkSource = new ContentDefinedChunkSourceFactory(13).Create(byteSource);

            var chunkSizes = new List<int>();

            while (chunkSource.Available())
            {
                var (buf, streamOffset, length) = chunkSource.Next();
                chunkSizes.Add(length);
                Console.WriteLine($"Split at {streamOffset + length}, preceding chunk size {length}");
                //Console.WriteLine($"  checksum window:{Encoding.ASCII.GetString(buf.AsSpan(length - RollingHash.WindowSize, RollingHash.WindowSize))}");
                //Console.WriteLine($"  {Encoding.ASCII.GetString(buf.AsSpan(0, length))}");
                //Console.WriteLine();
            }

            var expectedChunkSizes = new List<int> { 32205, 4451, 24151, 16975, 12126, 1942, 2391, 20775, 34712, 1942, 2391, 10482, 46947, 2391, 10482, 37291, 6090, 1244, 15195, 13597 };

            chunkSizes.Should().BeEquivalentTo(expectedChunkSizes);

            chunkSizes.Sum().Should().Be(bytes.Length);
        }
    }
}
