using System.Security.Cryptography;
using System.Text;

using FluentAssertions;

using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs.Tests
{
    public class DictionaryStorageTests
    {
        [Fact]
        public void TestUnversionedDictionaryStorage()
        {
            //var testDirectory = $"grufs/test-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";
            //var storage = new SftpStorageTests().GetSftpStorage(testDirectory);

            var storage = new InMemoryChunkStorage();
            try
            {
                var lookupKey = Encoding.ASCII.GetBytes("lookupkey");
                var strValue = "The quick brown fox";
                var value = Encoding.ASCII.GetBytes(strValue);

                var keyEncryptionKey = new KeyEncryptionKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                var addressKey = new HmacKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, addressKey, Compressor.None);

                var dictionaryStorage = new UnversionedDictionaryStorage(storage, chunkEncryptor);
                dictionaryStorage.TryPutValue(lookupKey, value, OverwriteStrategy.Deny).Should().Be(PutStatus.Success);
                dictionaryStorage.TryPutValue(lookupKey, value, OverwriteStrategy.Deny).Should().Be(PutStatus.OverwriteDenied); // Can't overwrite a key even if the value is the same
                dictionaryStorage.TryPutValue(lookupKey, value, OverwriteStrategy.Allow).Should().Be(PutStatus.Success); // Can't overwrite a key even if the value is the same

                dictionaryStorage.TryGetValue(lookupKey, out var retrievedValue).Should().BeTrue();

                strValue.Should().Be(Encoding.ASCII.GetString(retrievedValue.AsSpan()));

                // lookup with incorrect lookup key - item not found
                dictionaryStorage.TryGetValue(lookupKey.Take(1).ToArray(), out _).Should().BeFalse();

                // lookup with incorrect hmac key - item not found
                var addressKey2 = new HmacKey(Convert.FromHexString("1000000000000000000000000000000000000000000000000000000000000000"));
                var chunkEncryptor2 = new ChunkEncryptor(keyEncryptionKey, addressKey2, Compressor.None);
                new UnversionedDictionaryStorage(storage, chunkEncryptor2).TryGetValue(lookupKey, out _).Should().BeFalse();

                // lookup with incorrect key encryption key. Will fail to unwrap the wrapped key stored in the ciphertext.
                var keyEncryptionKey2 = new KeyEncryptionKey(Convert.FromHexString("1000000000000000000000000000000000000000000000000000000000000000"));
                var chunkEncryptor3 = new ChunkEncryptor(keyEncryptionKey2, addressKey, Compressor.None);
                new Action(() => new UnversionedDictionaryStorage(storage, chunkEncryptor3).TryGetValue(lookupKey, out _)).Should().ThrowExactly<CryptographicException>();
            }
            finally
            {
                //storage.DeleteDirectory(testDirectory);
            }
        }

        [Fact]
        public void TestVersionedDictionaryStorage()
        {
            //var testDirectory = $"grufs/test-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";
            //var storage = new SftpStorageTests().GetSftpStorage(testDirectory);

            var storage = new InMemoryChunkStorage();
            Func<int, byte[]> GetValue = i => Encoding.ASCII.GetBytes($"The quick brown fox-{i}");

            try
            {
                var keyEncryptionKey = new KeyEncryptionKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                var addressKey = new HmacKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, addressKey, Compressor.None);

                var keyNamespace = "asdf";

                var dictionaryStorage = new VersionedDictionaryStorage(keyNamespace, storage, chunkEncryptor);
                var lookupKeyBytes = Encoding.ASCII.GetBytes("lookupkey");

                dictionaryStorage.TryPutValue(lookupKeyBytes, 0, GetValue(0)).Should().BeTrue();
                dictionaryStorage.TryPutValue(lookupKeyBytes, 1, GetValue(1)).Should().BeTrue();
                dictionaryStorage.TryPutValue(lookupKeyBytes, 2, GetValue(2)).Should().BeTrue();

                // Cannot overwrite a versioned value
                dictionaryStorage.TryPutValue(lookupKeyBytes, 0, GetValue(0)).Should().BeFalse();

                dictionaryStorage.TryGetValue(lookupKeyBytes, 0, out var retrievedValue0).Should().BeTrue();
                dictionaryStorage.TryGetValue(lookupKeyBytes, 1, out var retrievedValue1).Should().BeTrue();
                dictionaryStorage.TryGetValue(lookupKeyBytes, 2, out var retrievedValue2).Should().BeTrue();

                retrievedValue0.AsSpan().ToArray().Should().BeEquivalentTo(GetValue(0));
                retrievedValue1.AsSpan().ToArray().Should().BeEquivalentTo(GetValue(1));
                retrievedValue2.AsSpan().ToArray().Should().BeEquivalentTo(GetValue(2));
            }
            finally
            {
                //storage.DeleteDirectory(testDirectory);
            }
        }

        [Fact]
        public void ValuesShouldEnumerate()
        {
            var storage = new InMemoryChunkStorage();
            Func<int, byte[]> GetValue = i => Encoding.ASCII.GetBytes($"The quick brown fox-{i}");

            var keyEncryptionKey = new KeyEncryptionKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
            var addressKey = new HmacKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
            var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, addressKey, Compressor.None);

            var keyNamespace = "collection:";

            var dictionaryStorage = new VersionedDictionaryStorage(keyNamespace, storage, chunkEncryptor);

            dictionaryStorage.Values(Encoding.UTF8.GetBytes("animals")).ToArray().Should().BeEmpty();

            dictionaryStorage.TryPutValue(Encoding.UTF8.GetBytes("animals"), 0, Encoding.UTF8.GetBytes("cat")).Should().Be(true);

            var values = dictionaryStorage.Values(Encoding.UTF8.GetBytes("animals")).ToArray();
            values[0].Item1.Should().Be(0L);
            values[0].Item2.AsSpan().ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes("cat"));

            dictionaryStorage.TryPutValue(Encoding.UTF8.GetBytes("animals"), 0, Encoding.UTF8.GetBytes("dog")).Should().Be(false);
            dictionaryStorage.TryPutValue(Encoding.UTF8.GetBytes("animals"), 1, Encoding.UTF8.GetBytes("dog")).Should().Be(true);

            values = dictionaryStorage.Values(Encoding.UTF8.GetBytes("animals")).ToArray();
            values[0].Item1.Should().Be(0L);
            values[0].Item2.AsSpan().ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes("cat"));
            values[1].Item1.Should().Be(1L);
            values[1].Item2.AsSpan().ToArray().Should().BeEquivalentTo(Encoding.UTF8.GetBytes("dog"));
        }
    }
}
