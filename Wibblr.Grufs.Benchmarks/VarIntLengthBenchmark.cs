using System.Numerics;

using BenchmarkDotNet.Attributes;

namespace Wibblr.Grufs.Benchmarks
{
    [SimpleJob]
    public class VarIntLengthBenchmark
    {
        //[Benchmark]
        //public int OldLengthUnsigned()
        //{
        //    int i = 123;
        //    var leadingZeroCount = (uint)BitOperations.LeadingZeroCount(unchecked((uint)i));
        //    return unchecked((int)(5 - ((leadingZeroCount + 3) / 7) + (leadingZeroCount / 32)));
        //}

        public int i = 12345;

        [Benchmark]
        public int NewLengthWithDivideUnsigned()
        {
            var leadingZeroCount = (uint)BitOperations.LeadingZeroCount(unchecked((uint)i));
            return unchecked((int)(5 - ((leadingZeroCount + (leadingZeroCount << 3) + 36) >> 6) + (leadingZeroCount / 32)));
        }

        [Benchmark]
        public int NewLengthWithBitShiftUnsigned()
        {
            var leadingZeroCount = (uint)BitOperations.LeadingZeroCount(unchecked((uint)i));
            return unchecked((int)(5 - ((leadingZeroCount + (leadingZeroCount << 3) + 36) >> 6) + (leadingZeroCount >> 5)));
        }

        //[Benchmark]
        //public int OldLengthUnsignedUnchecked(int i)
        //{
        //    var leadingZeroCount = unchecked((uint)BitOperations.LeadingZeroCount(unchecked((uint)i)));
        //    return unchecked((int)(5 - ((leadingZeroCount + 3) / 7) + (leadingZeroCount / 32)));
        //}

        [Benchmark]
        public int NewLengthWithDivideUnsignedUnchecked()
        {
            var leadingZeroCount = unchecked((uint)BitOperations.LeadingZeroCount(unchecked((uint)i)));
            return unchecked((int)(5 - ((leadingZeroCount + (leadingZeroCount << 3) + 36) >> 6) + (leadingZeroCount / 32)));
        }

        [Benchmark]
        public int NewLengthWithBitShiftUnsignedUnchecked()
        {
            var leadingZeroCount = unchecked((uint)BitOperations.LeadingZeroCount(unchecked((uint)i)));
            return unchecked((int)(5 - ((leadingZeroCount + (leadingZeroCount << 3) + 36) >> 6) + (leadingZeroCount >> 5)));
        }

        //[Benchmark]
        //public int OldLengthSigned(int i)
        //{
        //    var leadingZeroCount = (int)BitOperations.LeadingZeroCount(unchecked((uint)i));
        //    return 5 - ((leadingZeroCount + 3) / 7) + (leadingZeroCount / 32);
        //}

        [Benchmark]
        public int NewLengthWithDivideSigned()
        {
            var leadingZeroCount = (int)BitOperations.LeadingZeroCount(unchecked((uint)i));
            return 5 - ((leadingZeroCount + (leadingZeroCount << 3) + 36) >> 6) + (leadingZeroCount / 32);
        }

        [Benchmark]
        public int NewLengthWithBitShiftSigned()
        {
            var leadingZeroCount = (int)BitOperations.LeadingZeroCount(unchecked((uint)i));
            return 5 - ((leadingZeroCount + (leadingZeroCount << 3) + 36) >> 6) + (leadingZeroCount >> 5);
        }
    }
}