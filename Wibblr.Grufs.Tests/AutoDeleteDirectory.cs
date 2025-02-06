using System.Security.Cryptography;

namespace Wibblr.Grufs.Tests
{
    internal sealed class AutoDeleteDirectory : IDisposable
    {
        private static readonly string _ramdiskTempPath = "r:\\temp";

        public string Path { get; init; }

        public AutoDeleteDirectory()
        {
            var tempDirectory = Directory.Exists(_ramdiskTempPath) ? _ramdiskTempPath : System.IO.Path.GetTempPath();
            Path = System.IO.Path.Join(tempDirectory, "grufs", Utils.GetUniquifier());
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            Directory.Delete(Path, true);
        }
    }
}
