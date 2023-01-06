namespace Wibblr.Grufs
{
    public enum CompressionAlgorithm
    {
        Unknown = 0,
        None = 1,
        Brotli = 2,
        Deflate = 3,
        Gzip = 4,
        Zlib = 5,

        // Maximum value of this enum is 255, as it is truncated to a byte
    }
}