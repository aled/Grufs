namespace Wibblr.Grufs.Storage
{
    public enum CreateDirectoryStatus
    {
        Unknown = 0,
        Success = 1,
        AlreadyExists = 2,
        NonDirectoryAlreadyExists = 3,
        PathNotFound = 4,
        ConnectionError = 5,
        PermissionError = 6,
        UnknownError = 7,
    }
}