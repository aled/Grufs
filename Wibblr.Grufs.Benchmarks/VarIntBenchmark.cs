using System.Numerics;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace Wibblr.Grufs.Benchmarks
{
    [SimpleJob]
    public class VarIntBenchmark
    {
        //static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(VarIntBenchmark).Assembly).Run(args);
        static void Main(string[] args) => BenchmarkRunner.Run<VarLongBenchmark>();

        [Benchmark]
        public int OldLengthMethod()
        {
            int i = 123;
            int leadingZeroCount = BitOperations.LeadingZeroCount(unchecked((uint)i));
            return 5 - ((leadingZeroCount + 3) / 7) + (leadingZeroCount / 32);
        }

        [Benchmark]
        public int NewLengthMethod()
        {
            int i = 123;
            int leadingZeroCount = BitOperations.LeadingZeroCount(unchecked((uint)i));
            return 5 - ((leadingZeroCount + (leadingZeroCount << 3) + 36) >> 6) + (leadingZeroCount >> 5);
        }
    }

    [SimpleJob]
    public class VarLongBenchmark
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