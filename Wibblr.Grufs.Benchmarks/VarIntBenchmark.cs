using System.Numerics;

using BenchmarkDotNet.Attributes;

using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Benchmarks
{
    [SimpleJob]
    [MinIterationTime(1000)]
    [MinIterationCount(1000)]
    [MaxIterationCount(10000)]
    public class VarIntBenchmark
    {
        BufferBuilder builder = new BufferBuilder(50);

        [IterationSetup]
        public void IterationSetup()
        {
            builder.Clear();
        }

        [Benchmark]
        public void Serialize10()
        {
            // < 2^7
            new VarInt(10).SerializeTo(builder);
        }

        [Benchmark]
        public void Serialize10_new()
        {
            var Value = new VarInt(10).Value;

            int leadingZeroCount = BitOperations.LeadingZeroCount(unchecked((uint)Value));

            if (leadingZeroCount >= 25)
            {
                builder.AppendByte((byte)Value);
            }
            else if (leadingZeroCount >= 18)
            {
                builder.AppendKnownLengthSpan([
                    (byte)(0b10000000 | Value >> 8),
                    (byte)Value
                ]);
            }
        }

        [Benchmark]
        public void Serialize10000()
        {
            // < 2^14
            new VarInt(10000).SerializeTo(builder);
        }

        [Benchmark]
        public void Serialize1000000()
        {
            // < 2^21
            new VarInt(1000000).SerializeTo(builder);
        }
        [Benchmark]
        public void Serialize100000000()
        { 
            // < 2^28
            new VarInt(100000000).SerializeTo(builder);
        }
        [Benchmark]
        public void Serialize1000000000()
        { 
            // < 2^31
            new VarInt(1000000000).SerializeTo(builder);
        }
    }
}