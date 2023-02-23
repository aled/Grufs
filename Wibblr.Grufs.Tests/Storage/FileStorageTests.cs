using System.Text;

using Wibblr.Grufs.Core;
using Wibblr.Grufs.Encryption;
using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Tests
{
    public class FileStorageTests_Local : FileStorageTests<TemporaryLocalStorage> { }
    //public class FileStorageTests_Sftp : FileStorageTests<TemporarySftpStorage> { }

    public abstract class FileStorageTests<T> where T : IFileStorageFactory, new()
    {
        [Fact]
        public void Upload()
        {
            using (T temporaryStorage = new())
            {
                var storage = temporaryStorage.GetFileStorage();

                var testData = Encoding.ASCII.GetBytes("Hello World!");

                storage.WriteFile("tests/00001/test00001", testData, OverwriteStrategy.Allow, createDirectories: true).Should().Be(WriteFileStatus.Success);
                storage.ReadFile("tests/00001/test00001", out var downloaded).Should().Be(ReadFileStatus.Success);

                Convert.ToHexString(testData).Should().Be(Convert.ToHexString(downloaded));
            }
        }

        [Fact]
        public void UploadChunk()
        {
            var keyEncryptionKey = new KeyEncryptionKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var hmacKey = new HmacKey("0000000000000000000000000000000000000000000000000000000000000000".ToBytes());
            var compressor = new Compressor(CompressionAlgorithm.None);
            var plaintext = "The quick brown fox jumps over the lazy dog.\n".Repeat(10); // approx 450KB
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            var stream = new MemoryStream(plaintextBytes);
            var decryptedStream = new MemoryStream();

            using (T temporaryStorage = new())
            {
                var storage = temporaryStorage.GetFileStorage();
                var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, hmacKey, compressor);
                var chunkSourceFactory = new FixedSizeChunkSourceFactory(128);
                var streamStorage = new StreamStorage(storage, chunkSourceFactory, chunkEncryptor);
                var (address, level, stats) = streamStorage.Write(stream);

                foreach (var decryptedBuffer in streamStorage.Read(level, address))
                {
                    decryptedStream.Write(decryptedBuffer.AsSpan());
                }
            }

            var decryptedText = Encoding.UTF8.GetString(decryptedStream.ToArray());

            decryptedText.Should().Be(plaintext);
        }

        [Fact]
        public void ListFiles()
        {
            using (T temporaryStorage = new())
            {
                var storage = temporaryStorage.GetFileStorage();

                storage.CreateDirectory("a/b/c", createParents: true);

                storage.WriteFile("/a/b/c/d.txt", new byte[] { 0 }, OverwriteStrategy.Deny).Should().Be(WriteFileStatus.Success);
                storage.WriteFile("a/b/e.txt", new byte[] { 0 }, OverwriteStrategy.Deny).Should().Be(WriteFileStatus.Success); ;
                storage.WriteFile("a/b/f.txt", new byte[] { 0 }, OverwriteStrategy.Deny).Should().Be(WriteFileStatus.Success); ;

                var list = storage.ListFiles("a/", recursive: true)
                    .Select(x => new StoragePath(x.Parts, '/'))
                    .Select(x => x.ToString())
                    .Should().BeEquivalentTo(new[] { "b/e.txt", "b/f.txt", "b/c/d.txt" });
            }
        }

        [Fact]
        public void ListAddresses()
        {
            using (T temporaryStorage = new())
            {
                var storage = (IChunkStorage)temporaryStorage.GetFileStorage();

                var address0 = new Address(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                var address1 = new Address(Convert.FromHexString("1000000000000000000000000000000000000000000000000000000000000000"));
                var address2 = new Address(Convert.FromHexString("2000000000000000000000000000000000000000000000000000000000000000"));
                var address3 = new Address(Convert.FromHexString("3000000000000000000000000000000000000000000000000000000000000000"));
                var content = new byte[] { 0 };

                storage.Put(new EncryptedChunk(address0, content), OverwriteStrategy.Deny);
                storage.Put(new EncryptedChunk(address1, content), OverwriteStrategy.Deny);
                storage.Put(new EncryptedChunk(address2, content), OverwriteStrategy.Deny);
                storage.Put(new EncryptedChunk(address3, content), OverwriteStrategy.Deny);

                storage.ListAddresses().Should().BeEquivalentTo(new[] { address0, address1, address2, address3 });
            }
        }
    }
}
