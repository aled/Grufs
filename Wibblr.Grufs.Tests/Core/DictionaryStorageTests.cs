using System.Reflection;
using System.Security.Cryptography;
using System.Text;

using Wibblr.Grufs.Core;
using Wibblr.Grufs.Encryption;
using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Tests.Core
{
    public class DictionaryStorageTests_InMemory : DictionaryStorageTests<TemporaryInMemoryStorage> { };
    public class DictionaryStorageTests_Sqlite : DictionaryStorageTests<TemporarySqliteStorage> { };
    public class DictionaryStorageTests_Local : DictionaryStorageTests<TemporaryLocalStorage> { };
    public class DictionaryStorageTests_Sftp : DictionaryStorageTests<TemporarySftpStorage> { };

    public abstract class DictionaryStorageTests<T> where T: IChunkStorageFactory, new()
    {
        [Fact]
        public void TestUnversionedDictionaryStorage()
        {
            try
            {
                using (T temporaryStorage = new())
                {
                    var storage = temporaryStorage.GetChunkStorage();
                    storage.Init();

                    var lookupKey = Encoding.ASCII.GetBytes("lookupkey");
                    var strValue = "The quick brown fox";
                    var value = Encoding.ASCII.GetBytes(strValue);

                    var keyEncryptionKey = new KeyEncryptionKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                    var addressKey = new HmacKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                    var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, addressKey, Compressor.None);

                    var dictionaryStorage = new UnversionedDictionary(storage, chunkEncryptor);
                    dictionaryStorage.TryPutValue(lookupKey, value, OverwriteStrategy.Deny).ShouldBe(PutStatus.Success);
                    dictionaryStorage.TryPutValue(lookupKey, value, OverwriteStrategy.Deny).ShouldBe(PutStatus.OverwriteDenied); // Can't overwrite a key even if the value is the same
                    dictionaryStorage.TryPutValue(lookupKey, value, OverwriteStrategy.Allow).ShouldBe(PutStatus.Success); // Can't overwrite a key even if the value is the same

                    dictionaryStorage.TryGetValue(lookupKey, out var retrievedValue).ShouldBeTrue();

                    strValue.ShouldBe(Encoding.ASCII.GetString(retrievedValue.AsSpan()));

                    // lookup with incorrect lookup key - item not found
                    dictionaryStorage.TryGetValue(lookupKey.Take(1).ToArray(), out _).ShouldBeFalse();

                    // lookup with incorrect hmac key - item not found
                    var addressKey2 = new HmacKey(Convert.FromHexString("1000000000000000000000000000000000000000000000000000000000000000"));
                    var chunkEncryptor2 = new ChunkEncryptor(keyEncryptionKey, addressKey2, Compressor.None);
                    new UnversionedDictionary(storage, chunkEncryptor2).TryGetValue(lookupKey, out _).ShouldBeFalse();

                    // lookup with incorrect key encryption key. Will fail to unwrap the wrapped key stored in the ciphertext.
                    var keyEncryptionKey2 = new KeyEncryptionKey(Convert.FromHexString("1000000000000000000000000000000000000000000000000000000000000000"));
                    var chunkEncryptor3 = new ChunkEncryptor(keyEncryptionKey2, addressKey, Compressor.None);
                    Should.Throw<CryptographicException>(() => new UnversionedDictionary(storage, chunkEncryptor3).TryGetValue(lookupKey, out _));
                }
            }
            catch (TargetInvocationException e) when (e.InnerException is MissingSftpCredentialsException)
            {
                Console.WriteLine("Skipping test due to missing SFTP credentials");
            }
        }

        [Fact]
        public void TestVersionedDictionaryStorage()
        {
            try
            {
                using (T temporaryStorage = new())
                {
                    var storage = temporaryStorage.GetChunkStorage();
                    storage.Init();

                    Func<int, byte[]> GetValue = i => Encoding.ASCII.GetBytes($"The quick brown fox-{i}");

                    var keyEncryptionKey = new KeyEncryptionKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                    var addressKey = new HmacKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                    var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, addressKey, Compressor.None);

                    var keyNamespace = "asdf";

                    var dictionaryStorage = new VersionedDictionary(keyNamespace, storage, chunkEncryptor);
                    var lookupKeyBytes = Encoding.ASCII.GetBytes("lookupkey");

                    dictionaryStorage.TryPutValue(lookupKeyBytes, 0, GetValue(0)).ShouldBeTrue();
                    dictionaryStorage.TryPutValue(lookupKeyBytes, 1, GetValue(1)).ShouldBeTrue();
                    dictionaryStorage.TryPutValue(lookupKeyBytes, 2, GetValue(2)).ShouldBeTrue();

                    // Cannot overwrite a versioned value
                    dictionaryStorage.TryPutValue(lookupKeyBytes, 0, GetValue(0)).ShouldBeFalse();

                    dictionaryStorage.TryGetValue(lookupKeyBytes, 0, out var retrievedValue0).ShouldBeTrue();
                    dictionaryStorage.TryGetValue(lookupKeyBytes, 1, out var retrievedValue1).ShouldBeTrue();
                    dictionaryStorage.TryGetValue(lookupKeyBytes, 2, out var retrievedValue2).ShouldBeTrue();

                    retrievedValue0.AsSpan().ToArray().ShouldBeEquivalentTo(GetValue(0));
                    retrievedValue1.AsSpan().ToArray().ShouldBeEquivalentTo(GetValue(1));
                    retrievedValue2.AsSpan().ToArray().ShouldBeEquivalentTo(GetValue(2));
                }
            }
            catch (TargetInvocationException e) when (e.InnerException is MissingSftpCredentialsException)
            {
                Console.WriteLine("Skipping test due to missing SFTP credentials");
            }
        }

        [Fact]
        public void ValuesShouldEnumerate()
        {
            try
            {
                using (T temporaryStorage = new())
                {
                    var storage = temporaryStorage.GetChunkStorage();
                    storage.Init();

                    Func<int, byte[]> GetValue = i => Encoding.ASCII.GetBytes($"The quick brown fox-{i}");

                    var keyEncryptionKey = new KeyEncryptionKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                    var addressKey = new HmacKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                    var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, addressKey, Compressor.None);

                    var keyNamespace = "collection:";

                    var dictionaryStorage = new VersionedDictionary(keyNamespace, storage, chunkEncryptor);

                    dictionaryStorage.Values(Encoding.UTF8.GetBytes("animals")).ToArray().ShouldBeEmpty();

                    dictionaryStorage.TryPutValue(Encoding.UTF8.GetBytes("animals"), 0, Encoding.UTF8.GetBytes("cat")).ShouldBe(true);

                    var values = dictionaryStorage.Values(Encoding.UTF8.GetBytes("animals")).ToArray();
                    values[0].Item1.ShouldBe(0L);
                    values[0].Item2.AsSpan().ToArray().ShouldBeEquivalentTo(Encoding.UTF8.GetBytes("cat"));

                    dictionaryStorage.TryPutValue(Encoding.UTF8.GetBytes("animals"), 0, Encoding.UTF8.GetBytes("dog")).ShouldBe(false);
                    dictionaryStorage.TryPutValue(Encoding.UTF8.GetBytes("animals"), 1, Encoding.UTF8.GetBytes("dog")).ShouldBe(true);

                    values = dictionaryStorage.Values(Encoding.UTF8.GetBytes("animals")).ToArray();
                    values[0].Item1.ShouldBe(0L);
                    values[0].Item2.AsSpan().ToArray().ShouldBeEquivalentTo(Encoding.UTF8.GetBytes("cat"));
                    values[1].Item1.ShouldBe(1L);
                    values[1].Item2.AsSpan().ToArray().ShouldBeEquivalentTo(Encoding.UTF8.GetBytes("dog"));
                }
            }
            catch (TargetInvocationException e) when (e.InnerException is MissingSftpCredentialsException)
            {
                Console.WriteLine("Skipping test due to missing SFTP credentials");
            }
        }
    }
}
