using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO.Compression;

using Xunit;
using FluentAssertions;

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

            var animalsStorage = repository.GetCollectionStorage("animals");



            // var collectionStorage = new CollectionStorage(dictionaryStorage, "animals:");

            // collectionStorage.PrepareUpdate(Encoding.UTF8.GetBytes("cat"), Encoding.UTF8.GetBytes("colour:tortoiseshell"));
        }
    }
}
