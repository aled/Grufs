using System.IO.Compression;

namespace Wibblr.Grufs.Core
{
    public class Compressor
    {
        public static Compressor None => new Compressor(CompressionAlgorithm.None);

        public CompressionAlgorithm Algorithm { get; init; }
        public CompressionLevel Level { get; init; }

        public Compressor(CompressionAlgorithm algorithm, CompressionLevel level = CompressionLevel.Optimal)
        {
            Algorithm = algorithm;
            Level = level;
        }

        public Stream GetCompressionStream(Stream outputStream)
        {
            return Algorithm switch
            {
                CompressionAlgorithm.Brotli => new BrotliStream(outputStream, Level),
                CompressionAlgorithm.Deflate => new DeflateStream(outputStream, Level),
                CompressionAlgorithm.Gzip => new GZipStream(outputStream, Level),
                CompressionAlgorithm.Zlib => new ZLibStream(outputStream, Level),
                _ => throw new Exception("Unsupported compression algorithm")
            };
        }

        public Stream GetDecompressionStream(Stream inputStream)
        {
            return Algorithm switch
            {
                CompressionAlgorithm.Brotli => new BrotliStream(inputStream, CompressionMode.Decompress),
                CompressionAlgorithm.Deflate => new DeflateStream(inputStream, CompressionMode.Decompress),
                CompressionAlgorithm.Gzip => new GZipStream(inputStream, CompressionMode.Decompress),
                CompressionAlgorithm.Zlib => new ZLibStream(inputStream, CompressionMode.Decompress),
                _ => throw new Exception("Unsupported compression algorithm")
            };
        }

        public ReadOnlySpan<byte> Compress(ReadOnlySpan<byte> data, out CompressionAlgorithm algorithm)
        {
            algorithm = Algorithm;

            if (algorithm == CompressionAlgorithm.None)
            {
                return data;
            }

            using (var outputStream = new MemoryStream())
            {
                using (var compressedStream = GetCompressionStream(outputStream))
                {
                    compressedStream.Write(data);
                }
                var compressedBytes = outputStream.ToArray();

                if (compressedBytes.Length >= data.Length * 0.9f)
                {
                    //Log.WriteLine(0, $"Compression ratio >= 0.9 ({data.Length} to {compressedBytes.Length} bytes); ignoring compression algorithm");
                    algorithm = CompressionAlgorithm.None;
                    return data;
                }
                else
                {
                    //Log.WriteLine(0, $"Compressed chunk ({data.Length} to {compressedBytes.Length} bytes)");
                    return compressedBytes;
                }
            }
        }

        public byte[] Decompress(byte[] data, int length)
        {
            if (Algorithm == CompressionAlgorithm.None)
            {
                return data;
            }

            using (var inputStream = new MemoryStream(data, 0, length))
            using (var outputStream = new MemoryStream())
            {
                using (var compressedStream = GetDecompressionStream(inputStream))
                {
                    compressedStream.CopyTo(outputStream);
                }
                return outputStream.ToArray();
            }
        }
    }
}