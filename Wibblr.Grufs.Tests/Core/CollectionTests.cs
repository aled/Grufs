using System.IO.Compression;
using System.Reflection;
using System.Text;

using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Tests.Core
{
    public class CollectionTests_InMemory : CollectionTests<TemporaryInMemoryStorage> { };

    public class CollectionTests_Sqlite : CollectionTests<TemporarySqliteStorage> { };

    public class CollectionTests_Local : CollectionTests<TemporaryLocalStorage> { };

    public class CollectionTests_Sftp : CollectionTests<TemporarySftpStorage> { };

    public abstract class CollectionTests<T> where T : IChunkStorageFactory, new()
    {
        [Fact]
        public void TestCollectionStorage()
        {
            try
            {
                using (T temporaryStorage = new())
                {
                    var storage = temporaryStorage.GetChunkStorage();
                    var repository = new Repository("myrepo", storage, "asdf");
                    repository.Initialize(compressor: new Compressor(CompressionAlgorithm.Brotli, CompressionLevel.Optimal));

                    var animalsStorage = repository.GetCollectionStorage("animals"); ;

                    animalsStorage.PrepareUpdate(Encoding.UTF8.GetBytes("cat1"), Encoding.UTF8.GetBytes("colour:tortoiseshell"));
                    animalsStorage.PrepareUpdate(Encoding.UTF8.GetBytes("cat100"), Encoding.UTF8.GetBytes("colour:ginger"));
                    animalsStorage.PrepareUpdate(Encoding.UTF8.GetBytes("cat10000"), Encoding.UTF8.GetBytes("colour:black"));
                    animalsStorage.PrepareUpdate(Encoding.UTF8.GetBytes("cat1000000"), Encoding.UTF8.GetBytes("colour:spotty"));

                    animalsStorage.WriteChanges(0);

                    var values = animalsStorage.Values().ToArray();

                    values.Select(x => Encoding.UTF8.GetString(x.AsSpan())).Should().BeEquivalentTo(new[]
                    {
                        "colour:tortoiseshell",
                        "colour:ginger",
                        "colour:black",
                        "colour:spotty"
                    });

                    animalsStorage.PrepareDelete(Encoding.UTF8.GetBytes("cat100"));
                    animalsStorage.WriteChanges(0);

                    animalsStorage.Values().Select(x => Encoding.UTF8.GetString(x.AsSpan())).Should().BeEquivalentTo(new[]
                    {
                        "colour:tortoiseshell",
                        "colour:black",
                        "colour:spotty"
                    });
                }
            }
            catch (TargetInvocationException e) when (e.InnerException is MissingSftpCredentialsException)
            {
                Console.WriteLine("Skipping test due to missing SFTP credentials");
            }
        }
    }
}
