using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Tests.Core
{
    public class RepositoryTests_InMemory : RepositoryTests<TemporaryInMemoryStorage> { };
    public class RepositoryTests_Sqlite : RepositoryTests<TemporarySqliteStorage> { }; 
    public class RepositoryTests_Local : RepositoryTests<TemporaryLocalStorage> { };
    //public class RepositoryTests_Sftp : RepositoryTests<TemporarySftpStorage> { };

    public abstract class RepositoryTests<T> where T : IChunkStorageFactory, new()
    {
        [Fact]
        public void RepositoryInitAndOpen()
        {
            using (T temporaryStorage = new())
            {
                var storage = temporaryStorage.GetChunkStorage();
                var r1 = new Repository("myrepo", storage, "hello");
                r1.Initialize();

                var r2 = new Repository("myrepo", storage, "hello");
                r2.Open();
                r1.MasterKey.ToString().Should().Be(r2.MasterKey.ToString());
            }
        }

        // Test filename translation
    }
}
