namespace Wibblr.Grufs.Storage
{
    public enum WriteFileStatus
    {
        Unknown = 0,
        Success = 1,
        OverwriteDenied= 2,
        PathNotFound = 4,
        Error = 5,
    }
}