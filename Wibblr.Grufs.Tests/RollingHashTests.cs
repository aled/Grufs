using System;
using System.Text;

using FluentAssertions;

namespace Wibblr.Grufs.Tests
{
    public class RollingHashTests
    {
        [Fact]
        public void ShouldThrowWhenInitalArrayIsNotWindowSize()
        {
            new Action(() => new RollingHash(new byte[2])).Should().Throw<ArgumentException>();
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

            values[0].Should().Be(12717692u);

            for (int i = RollingHash.WindowSize; i < bytes.Length; i++)
            {
                hash.Append(bytes[i - RollingHash.WindowSize], bytes[i]);
                values.Add(hash.Value);
                Console.WriteLine(hash.Value);
            }

            values[bytes.Length - RollingHash.WindowSize].Should().Be(6492841u);
        }
    }

}
