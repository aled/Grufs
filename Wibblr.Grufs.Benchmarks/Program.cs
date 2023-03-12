using BenchmarkDotNet.Running;

namespace Wibblr.Grufs.Benchmarks
{
    public class Program
    {
        static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(VarIntBenchmark).Assembly).Run(args);
    }
}