using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using FluentAssertions;

using Wibblr.Grufs.Encryption;

using Xunit;

namespace Wibblr.Grufs.Tests
{
    public class DictionaryStorageTests
    {
        [Fact]
        public void TestDictionaryStorage()
        {
            //var testDirectory = $"grufs/test-{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";
            //var storage = new SftpStorageTests().GetSftpStorage(testDirectory);

            var storage = new InMemoryChunkStorage();
            try
            {
                var dictionaryStorage = new UnversionedDictionaryStorage(storage);

                var lookupKeyBytes = Encoding.ASCII.GetBytes("lookupkey");
                var value = "The quick brown fox";
                var valueBytes = Encoding.ASCII.GetBytes(value);

                var keyEncryptionKey = new KeyEncryptionKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                var addressKey = new HmacKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));

                dictionaryStorage.TryPutValue(keyEncryptionKey, addressKey, lookupKeyBytes, valueBytes, OverwriteStrategy.DenyWithError).Should().BeTrue();

                // Can't overwrite a key even if the value is the same
                dictionaryStorage.TryPutValue(keyEncryptionKey, addressKey, lookupKeyBytes, valueBytes, OverwriteStrategy.DenyWithError).Should().BeFalse();
                dictionaryStorage.TryGetValue(keyEncryptionKey, addressKey, lookupKeyBytes, out var retrievedValue).Should().BeTrue();

                value.Should().Be(Encoding.ASCII.GetString(retrievedValue));

                // lookup with incorrect lookup key - item not found
                dictionaryStorage.TryGetValue(keyEncryptionKey, addressKey, lookupKeyBytes.Take(1).ToArray(), out _).Should().BeFalse();

                // lookup with incorrect hmac key - item not found
                var addressKey2 = new HmacKey(Convert.FromHexString("1000000000000000000000000000000000000000000000000000000000000000"));
                dictionaryStorage.TryGetValue(keyEncryptionKey, addressKey2, lookupKeyBytes, out _).Should().BeFalse();

                // lookup with incorrect key encryption key. Will fail to unwrap the wrapped key stored in the ciphertext.
                var keyEncryptionKey2 = new KeyEncryptionKey(Convert.FromHexString("1000000000000000000000000000000000000000000000000000000000000000"));
                new Action(() => dictionaryStorage.TryGetValue(keyEncryptionKey2, addressKey, lookupKeyBytes, out _)).Should().ThrowExactly<CryptographicException>();
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
                var dictionaryStorage = new VersionedDictionaryStorage(storage);
                var lookupKeyBytes = Encoding.ASCII.GetBytes("lookupkey");

                var keyEncryptionKey = new KeyEncryptionKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
                var addressKey = new HmacKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));

                dictionaryStorage.TryPutValue(keyEncryptionKey, addressKey, lookupKeyBytes, 0, GetValue(0)).Should().BeTrue();
                dictionaryStorage.TryPutValue(keyEncryptionKey, addressKey, lookupKeyBytes, 1, GetValue(1)).Should().BeTrue();
                dictionaryStorage.TryPutValue(keyEncryptionKey, addressKey, lookupKeyBytes, 2, GetValue(2)).Should().BeTrue();

                // Cannot overwrite a versioned value
                dictionaryStorage.TryPutValue(keyEncryptionKey, addressKey, lookupKeyBytes, 0, GetValue(0)).Should().BeFalse();

                dictionaryStorage.TryGetValue(keyEncryptionKey, addressKey, lookupKeyBytes, 0, out var retrievedValue0).Should().BeTrue();
                dictionaryStorage.TryGetValue(keyEncryptionKey, addressKey, lookupKeyBytes, 1, out var retrievedValue1).Should().BeTrue();
                dictionaryStorage.TryGetValue(keyEncryptionKey, addressKey, lookupKeyBytes, 2, out var retrievedValue2).Should().BeTrue();

                retrievedValue0.AsSpan().ToArray().Should().BeEquivalentTo(GetValue(0));
                retrievedValue1.AsSpan().ToArray().Should().BeEquivalentTo(GetValue(1));
                retrievedValue2.AsSpan().ToArray().Should().BeEquivalentTo(GetValue(2));
            }
            finally
            {
                //storage.DeleteDirectory(testDirectory);
            }
        }

    }
}
