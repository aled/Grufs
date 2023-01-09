using System.IO.Compression;
using System.Text;

using FluentAssertions;

using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Tests
{
    public class CollectionStorageTests
    {
        [Fact]
        public void TestCollectionStorage()
        {
            var chunkStorage = new InMemoryChunkStorage();

            var repository = new Repository(chunkStorage);
            repository.Initialize("asdf", compressor: new Compressor(CompressionAlgorithm.Brotli, CompressionLevel.Optimal));

            var animalsStorage = repository.GetCollectionStorage("animals");;

            animalsStorage.PrepareUpdate(Encoding.UTF8.GetBytes("cat10"), Encoding.UTF8.GetBytes("colour:tortoiseshell"));
            animalsStorage.PrepareUpdate(Encoding.UTF8.GetBytes("cat20"), Encoding.UTF8.GetBytes("colour:ginger"));
            animalsStorage.PrepareUpdate(Encoding.UTF8.GetBytes("cat30"), Encoding.UTF8.GetBytes("colour:black"));
            animalsStorage.PrepareUpdate(Encoding.UTF8.GetBytes("cat40"), Encoding.UTF8.GetBytes("colour:spotty"));

            animalsStorage.WriteChanges(0);

            var values = animalsStorage.Values().ToArray();

            values.Select(x => Encoding.UTF8.GetString(x.AsSpan())).Should().BeEquivalentTo(new[]
            {
                "colour:tortoiseshell",
                "colour:ginger",
                "colour:black",
                "colour:spotty"
            });


            animalsStorage.PrepareDelete(Encoding.UTF8.GetBytes("cat20"));
            animalsStorage.WriteChanges(0);

            animalsStorage.Values().Select(x => Encoding.UTF8.GetString(x.AsSpan())).Should().BeEquivalentTo(new[]
            {
                "colour:tortoiseshell",
                "colour:black",
                "colour:spotty"
            });

        }
    }
}
