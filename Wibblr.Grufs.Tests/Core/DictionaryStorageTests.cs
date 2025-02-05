using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

using Shouldly;

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
        public async Task TestUnversionedDictionaryStorage()
        {
            CancellationToken token = CancellationToken.None;
            try
            {
                using (T temporaryStorage = new())
                {
                    var storage = temporaryStorage.GetChunkStorage();
                    await storage.InitAsync(token);

                    var lookupKey = Encoding.ASCII.GetBytes("lookupkey").ToImmutableArray();
                    var strValue = "The quick brown fox";
                    var value = new  ArrayBuffer(Encoding.ASCII.GetBytes(strValue));

                    var keyEncryptionKey = new KeyEncryptionKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                    var addressKey = new HmacKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                    var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, addressKey, Compressor.None);

                    var dictionaryStorage = new UnversionedDictionary(storage, chunkEncryptor);
                    (await dictionaryStorage.PutValueAsync(lookupKey, value, OverwriteStrategy.Deny, token)).ShouldBe(PutStatus.Success);
                    (await dictionaryStorage.PutValueAsync(lookupKey, value, OverwriteStrategy.Deny, token)).ShouldBe(PutStatus.OverwriteDenied); // Can't overwrite a key even if the value is the same
                    (await dictionaryStorage.PutValueAsync(lookupKey, value, OverwriteStrategy.Allow, token)).ShouldBe(PutStatus.Success); // Can't overwrite a key even if the value is the same

                    var actual = Encoding.ASCII.GetString((await dictionaryStorage.GetValueAsync(lookupKey, token)).AsSpan());
                    actual.ShouldBe(strValue);

                    // lookup with incorrect lookup key - item not found
                    (await dictionaryStorage.GetValueAsync(lookupKey.Take(1).ToImmutableArray(), token)).ShouldBe(ArrayBuffer.Empty);

                    // lookup with incorrect hmac key - item not found
                    var addressKey2 = new HmacKey(Convert.FromHexString("1000000000000000000000000000000000000000000000000000000000000000"));
                    var chunkEncryptor2 = new ChunkEncryptor(keyEncryptionKey, addressKey2, Compressor.None);
                    (await new UnversionedDictionary(storage, chunkEncryptor2).GetValueAsync(lookupKey, token)).ShouldBe(ArrayBuffer.Empty);

                    // lookup with incorrect key encryption key. Will fail to unwrap the wrapped key stored in the ciphertext.
                    var keyEncryptionKey2 = new KeyEncryptionKey(Convert.FromHexString("1000000000000000000000000000000000000000000000000000000000000000"));
                    var chunkEncryptor3 = new ChunkEncryptor(keyEncryptionKey2, addressKey, Compressor.None);
                    Should.Throw<CryptographicException>(async () => await new UnversionedDictionary(storage, chunkEncryptor3).GetValueAsync(lookupKey, token));
                }
            }
            catch (TargetInvocationException e) when (e.InnerException is MissingSftpCredentialsException)
            {
                Console.WriteLine("Skipping test due to missing SFTP credentials");
            }
        }

        [Fact]
        public async Task TestVersionedDictionaryStorage()
        {
            var token = CancellationToken.None;
            try
            {
                using (T temporaryStorage = new())
                {
                    var storage = temporaryStorage.GetChunkStorage();
                    await storage.InitAsync(token);

                    Func<int, ArrayBuffer> GetValue = i => new ArrayBuffer(Encoding.ASCII.GetBytes($"The quick brown fox-{i}"));

                    var keyEncryptionKey = new KeyEncryptionKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                    var addressKey = new HmacKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                    var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, addressKey, Compressor.None);

                    var keyNamespace = "asdf";

                    var dictionaryStorage = new VersionedDictionary(keyNamespace, storage, chunkEncryptor);
                    var lookupKeyBytes = Encoding.ASCII.GetBytes("lookupkey");

                    (await dictionaryStorage.PutValueAsync(lookupKeyBytes, 0, GetValue(0), token)).ShouldBeTrue();
                    (await dictionaryStorage.PutValueAsync(lookupKeyBytes, 1, GetValue(1), token)).ShouldBeTrue();
                    (await dictionaryStorage.PutValueAsync(lookupKeyBytes, 2, GetValue(2), token)).ShouldBeTrue();

                    // Cannot overwrite a versioned value
                    (await dictionaryStorage.PutValueAsync(lookupKeyBytes, 0, GetValue(0), token)).ShouldBeFalse();

                    (await dictionaryStorage.GetValueAsync(lookupKeyBytes, 0, token)).AsSpan().ToArray().ShouldBeEquivalentTo(GetValue(0).AsSpan().ToArray());
                    (await dictionaryStorage.GetValueAsync(lookupKeyBytes, 1, token)).AsSpan().ToArray().ShouldBeEquivalentTo(GetValue(1).AsSpan().ToArray());
                    (await dictionaryStorage.GetValueAsync(lookupKeyBytes, 2, token)).AsSpan().ToArray().ShouldBeEquivalentTo(GetValue(2).AsSpan().ToArray());
                }
            }
            catch (TargetInvocationException e) when (e.InnerException is MissingSftpCredentialsException)
            {
                Console.WriteLine("Skipping test due to missing SFTP credentials");
            }
        }

        [Fact]
        public async Task ValuesShouldEnumerate()
        {
            var token = CancellationToken.None;
            try
            {
                using (T temporaryStorage = new())
                {
                    var storage = temporaryStorage.GetChunkStorage();
                    await storage.InitAsync(token);

                    Func<int, ArrayBuffer> GetValue = i => new ArrayBuffer(Encoding.ASCII.GetBytes($"The quick brown fox-{i}"));

                    var keyEncryptionKey = new KeyEncryptionKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                    var addressKey = new HmacKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                    var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, addressKey, Compressor.None);

                    var keyNamespace = "collection:";
                    var lookupKey = Encoding.UTF8.GetBytes("animals");
                    var dictionaryStorage = new VersionedDictionary(keyNamespace, storage, chunkEncryptor);

                    (await dictionaryStorage.ValuesAsync(lookupKey, token).ToArrayAsync()).ShouldBeEmpty();

                    (await dictionaryStorage.PutValueAsync(lookupKey, 0, new ArrayBuffer(Encoding.UTF8.GetBytes("cat")), token)).ShouldBe(true);

                    var values = await dictionaryStorage.ValuesAsync(lookupKey, token).ToArrayAsync();
                    values[0].sequenceNumber.ShouldBe(0L);
                    values[0].value.AsSpan().ToArray().ShouldBeEquivalentTo(Encoding.UTF8.GetBytes("cat"));

                    (await dictionaryStorage.PutValueAsync(lookupKey, 0, new ArrayBuffer(Encoding.UTF8.GetBytes("dog")), token)).ShouldBe(false);
                    (await dictionaryStorage.PutValueAsync(lookupKey, 1, new ArrayBuffer(Encoding.UTF8.GetBytes("dog")), token)).ShouldBe(true);

                    values = await dictionaryStorage.ValuesAsync(lookupKey, token).ToArrayAsync();
                    values[0].sequenceNumber.ShouldBe(0L);
                    values[0].value.AsSpan().ToArray().ShouldBeEquivalentTo(Encoding.UTF8.GetBytes("cat"));
                    values[1].sequenceNumber.ShouldBe(1L);
                    values[1].value.AsSpan().ToArray().ShouldBeEquivalentTo(Encoding.UTF8.GetBytes("dog"));
                }
            }
            catch (TargetInvocationException e) when (e.InnerException is MissingSftpCredentialsException)
            {
                Console.WriteLine("Skipping test due to missing SFTP credentials");
            }
        }
    }
}
