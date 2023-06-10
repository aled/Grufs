namespace Wibblr.Grufs.Storage
{
    public enum ReadFileStatus
    {
        Unknown = 0,
        Success = 1,
        PathNotFound = 2,
        ConnectionError = 3,
        UnknownError = 4,
    }
}