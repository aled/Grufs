namespace Wibblr.Grufs.Tests
{
    internal class TemporaryFileStorageFactory
    {
        public ITemporaryFileStorage GetTemporaryFileStorage(TemporaryFileStorageFactoryType type)
        {
            return type switch
            {
                TemporaryFileStorageFactoryType.Directory => new TemporaryDirectoryStorage(),
                TemporaryFileStorageFactoryType.Sftp => new TemporarySftpStorage(),
                _ => throw new Exception()
            };
        }
    }
}
