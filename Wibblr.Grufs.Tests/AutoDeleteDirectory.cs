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
            var uniquifier = $"{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";
            Path = System.IO.Path.Join(tempDirectory, "grufs", uniquifier);
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            Directory.Delete(Path, true);
        }
    }
}
