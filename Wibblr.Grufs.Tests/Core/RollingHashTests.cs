using System.Text;

namespace Wibblr.Grufs.Tests
{
    public class RollingHashTests
    {
        [Fact]
        public void ShouldThrowWhenInitalArrayIsNotWindowSize()
        {
            Should.Throw<ArgumentException>(() => new RollingHash(new byte[2]));
        }

        [Fact]
        public void ShouldYieldKnownHashes()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 2; i++)
                sb.Append($"{i} - The quick brown fox jumps over the lazy dog - {i}...");

            var bytes = Encoding.ASCII.GetBytes(sb.ToString());
            var hash = new RollingHash(bytes.AsSpan(0, RollingHash.WindowSize));

            var values = new List<uint>();
            values.Add(hash.Value);

            values[0].ShouldBe(12717692u);

            for (int i = RollingHash.WindowSize; i < bytes.Length; i++)
            {
                hash.Roll(bytes[i - RollingHash.WindowSize], bytes[i]);
                values.Add(hash.Value);
                Log.WriteLine(0, hash.Value.ToString());
            }

            values[bytes.Length - RollingHash.WindowSize].ShouldBe(6492841u);
        }
    }
}
