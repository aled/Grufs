using System.Reflection;

using Newtonsoft.Json.Linq;

using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Tests.Core
{
    public class RepositoryTests_InMemory : RepositoryTests<TemporaryInMemoryStorage> { };
    public class RepositoryTests_Sqlite : RepositoryTests<TemporarySqliteStorage> { }; 
    public class RepositoryTests_Local : RepositoryTests<TemporaryLocalStorage> { };
    public class RepositoryTests_Sftp : RepositoryTests<TemporarySftpStorage> { };

    public abstract class RepositoryTests<T> where T : IChunkStorageFactory, new()
    {
        [Fact]
        public async Task RepositoryInitAndOpen()
        {
            var token = CancellationToken.None;
            try
            { 
                using (T temporaryStorage = new())
                {
                    var storage = temporaryStorage.GetChunkStorage();
                    await storage.InitAsync(token);

                    var r1 = new Repository("myrepo", storage, "hello");
                    await r1.InitializeAsync(token);

                    var r2 = new Repository("myrepo", storage, "hello");
                    await r2.OpenAsync(token);
                    r1.MasterKey.ToString().ShouldBe(r2.MasterKey.ToString());
                }
            }
            catch (TargetInvocationException e) when (e.InnerException is MissingSftpCredentialsException)
            {
                Console.WriteLine("Skipping test due to missing SFTP credentials");
            }
        }

        // Test filename translation
    }
}
