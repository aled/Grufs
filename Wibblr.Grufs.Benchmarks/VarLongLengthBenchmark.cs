using System.Numerics;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace Wibblr.Grufs.Benchmarks
{
    [SimpleJob]
    public class VarLongLengthBenchmark
    {
        [Benchmark]
        public int OldLengthMethod()
        {
            long i = 123;
            int leadingZeroCount = BitOperations.LeadingZeroCount(unchecked((ulong)i));
            return 9 - (Math.Clamp(leadingZeroCount - 1, 0, 64) / 7) + (leadingZeroCount / 64);
        }

        [Benchmark]
        public int NewLengthMethod()
        {
            long i = 123;
            int leadingZeroCount = BitOperations.LeadingZeroCount(unchecked((ulong)i));
            return 9 - ((leadingZeroCount - 9 + (leadingZeroCount << 3) + 9) >> 6) + (leadingZeroCount >> 6);
        }
    }
}