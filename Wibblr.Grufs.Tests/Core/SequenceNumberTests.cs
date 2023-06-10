using System.Text;

using Wibblr.Grufs.Core;
using Wibblr.Grufs.Encryption;
using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Tests
{
    public class SequenceNumberTests_InMemory : SequenceNumberTests<TemporaryInMemoryStorage>
    {
        public SequenceNumberTests_InMemory(SequenceNumberTestsFixture<TemporaryInMemoryStorage> fixture) : base(fixture) { }
    };

    public class SequenceNumberTests_Sqlite : SequenceNumberTests<TemporarySqliteStorage>
    {
        public SequenceNumberTests_Sqlite(SequenceNumberTestsFixture<TemporarySqliteStorage> fixture) : base(fixture) { }
    };

    public class SequenceNumberTests_Local : SequenceNumberTests<TemporaryLocalStorage>
    {
        public SequenceNumberTests_Local(SequenceNumberTestsFixture<TemporaryLocalStorage> fixture) : base(fixture) { }
    };

    // Currently SFTP is too slow to run this in a reasonable time.
    public class SequenceNumberTests_Sftp : SequenceNumberTests<TemporarySftpStorage>
    {
        public SequenceNumberTests_Sftp(SequenceNumberTestsFixture<TemporarySftpStorage> fixture) : base(fixture) { }
    };

    public class SequenceNumberTestsFixture<T> : IDisposable where T : IChunkStorageFactory, new()
    {
        private T temporaryStorage;
        private IChunkStorage _storage;
        private VersionedDictionary _dictionary;
        private KeyEncryptionKey _keyEncryptionKey = new KeyEncryptionKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
        private HmacKey _addressKey = new HmacKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
        private string _keyNamespace = nameof(SequenceNumberTests<T>);

        public SequenceNumberTestsFixture()
        {
            temporaryStorage = new();
            _storage = temporaryStorage.GetChunkStorage();
            var chunkEncryptor = new ChunkEncryptor(_keyEncryptionKey, _addressKey, Compressor.None);
            _dictionary = new VersionedDictionary(_keyNamespace, _storage, chunkEncryptor);

            // Add a bunch of values into the dictionary storage. For each of these IDs, insert that many versions of the value
            foreach (var lookupKeyId in new[] { 0, 1, 2, 10, 100, 1000})
            {
                var start = DateTime.Now;
                for (long sequence = 0; sequence < lookupKeyId; sequence++)
                {
                    var lookupKey = Encoding.ASCII.GetBytes($"lookupkey-{lookupKeyId}");
                    var value = Encoding.ASCII.GetBytes($"value-{lookupKeyId}-sequence-{sequence}");
                    _dictionary.TryPutValue(lookupKey, sequence, value).Should().BeTrue();
                }
                Log.WriteLine(0, $"Created test fixture for lookup key id {lookupKeyId} in {((DateTime.Now.Ticks - start.Ticks) / 10000)} ms");
            }
        }

        public long GetNextSequenceNumber(long lookupKeyId, long sequenceNumberHint)
        {
            var lookupKey = Encoding.ASCII.GetBytes($"lookupkey-{lookupKeyId}");
            var sequenceNumber = _dictionary.GetNextSequenceNumber(lookupKey, sequenceNumberHint);
            return sequenceNumber;
        }

        public (long sequenceNumber, int lookupCount) GetNextSequenceNumberAndLookupCount(long lookupKeyId, long sequenceNumberHint)
        {
            var lookupKey = Encoding.ASCII.GetBytes($"lookupkey-{lookupKeyId}");
            var sequenceNumber = _dictionary.GetNextSequenceNumber(lookupKey, sequenceNumberHint, out var lookupCount);
            return (sequenceNumber, lookupCount);
        }

        public void Dispose()
        {
            temporaryStorage.Dispose();
        }
    }

    public abstract class SequenceNumberTests<T> : IClassFixture<SequenceNumberTestsFixture<T>> where T : IChunkStorageFactory, new()
    {
        protected SequenceNumberTestsFixture<T> _fixture;
        protected Compressor _compressor = new Compressor(CompressionAlgorithm.None);

        public SequenceNumberTests(SequenceNumberTestsFixture<T> fixture) 
        {
            _fixture = fixture;
        }

        [SkippableFact(typeof(MissingSftpCredentialsException))]
        public void ShouldReturnSequenceNumberZeroForMissingKey()
        {
            _fixture.GetNextSequenceNumber(0, 0).Should().Be(0);
            _fixture.GetNextSequenceNumber(0, 1).Should().Be(0);
            _fixture.GetNextSequenceNumber(0, 2).Should().Be(0);
            _fixture.GetNextSequenceNumber(0, long.MaxValue).Should().Be(0);
            _fixture.GetNextSequenceNumber(0, long.MinValue).Should().Be(0);
        }

        [SkippableFact(typeof(MissingSftpCredentialsException))]
        public void ShouldHaveTwoLookupsWhenHintIsHighestExisting()
        {
            _fixture.GetNextSequenceNumberAndLookupCount(1, 0).Should().Be((1, 2));
            _fixture.GetNextSequenceNumberAndLookupCount(2, 1).Should().Be((2, 2));
            _fixture.GetNextSequenceNumberAndLookupCount(10, 9).Should().Be((10, 2));
            _fixture.GetNextSequenceNumberAndLookupCount(100, 99).Should().Be((100, 2));
            _fixture.GetNextSequenceNumberAndLookupCount(1000, 999).Should().Be((1000, 2));
        }

        [SkippableFact(typeof(MissingSftpCredentialsException))]
        public void ShouldHaveThreeLookupsWhenHintIsOneLessThanHighestExisting()
        {
            _fixture.GetNextSequenceNumberAndLookupCount(2, 0).Should().Be((2, 3));
            _fixture.GetNextSequenceNumberAndLookupCount(10, 8).Should().Be((10, 3));
            _fixture.GetNextSequenceNumberAndLookupCount(100, 98).Should().Be((100, 3));
            _fixture.GetNextSequenceNumberAndLookupCount(1000, 998).Should().Be((1000, 3));
        }

        [SkippableFact(typeof(MissingSftpCredentialsException))]
        public void ShouldHaveFiveLookupsWhenHintIsTwoLessThanHighestExisting()
        {
            _fixture.GetNextSequenceNumberAndLookupCount(10, 7).Should().Be((10, 5));
            _fixture.GetNextSequenceNumberAndLookupCount(100, 97).Should().Be((100, 5));
            _fixture.GetNextSequenceNumberAndLookupCount(1000, 997).Should().Be((1000, 5));
        }

        [SkippableFact(typeof(MissingSftpCredentialsException))]
        public void ShouldHaveFiveLookupsWhenHintIsThreeLessThanHighestExisting()
        {
            _fixture.GetNextSequenceNumberAndLookupCount(10, 6).Should().Be((10, 5));
            _fixture.GetNextSequenceNumberAndLookupCount(100, 96).Should().Be((100, 5));
            _fixture.GetNextSequenceNumberAndLookupCount(1000, 996).Should().Be((1000, 5));
        }

        [SkippableFact(typeof(MissingSftpCredentialsException))]
        public void ShouldWorkWhenHintIsTooHigh()
        {
            _fixture.GetNextSequenceNumber(0, 1).Should().Be(0);
            _fixture.GetNextSequenceNumber(0, 2).Should().Be(0);
            _fixture.GetNextSequenceNumber(0, 3).Should().Be(0);
            _fixture.GetNextSequenceNumber(0, long.MaxValue).Should().Be(0);

            _fixture.GetNextSequenceNumber(1, 2).Should().Be(1);
            _fixture.GetNextSequenceNumber(1, 3).Should().Be(1);
            _fixture.GetNextSequenceNumber(1, 4).Should().Be(1);
            _fixture.GetNextSequenceNumber(1, long.MaxValue).Should().Be(1);

            _fixture.GetNextSequenceNumber(2, 3).Should().Be(2);
            _fixture.GetNextSequenceNumber(2, 4).Should().Be(2);
            _fixture.GetNextSequenceNumber(2, 5).Should().Be(2);
            _fixture.GetNextSequenceNumber(2, long.MaxValue).Should().Be(2);

            _fixture.GetNextSequenceNumber(10, 11).Should().Be(10);
            _fixture.GetNextSequenceNumber(10, 12).Should().Be(10);
            _fixture.GetNextSequenceNumber(10, 13).Should().Be(10);
            _fixture.GetNextSequenceNumber(10, long.MaxValue).Should().Be(10);

            _fixture.GetNextSequenceNumber(100, 101).Should().Be(100);
            _fixture.GetNextSequenceNumber(100, 102).Should().Be(100);
            _fixture.GetNextSequenceNumber(100, 103).Should().Be(100);
            _fixture.GetNextSequenceNumber(100, long.MaxValue).Should().Be(100);

            _fixture.GetNextSequenceNumber(1000, 1001).Should().Be(1000);
            _fixture.GetNextSequenceNumber(1000, 1002).Should().Be(1000);
            _fixture.GetNextSequenceNumber(1000, 1003).Should().Be(1000);
            _fixture.GetNextSequenceNumber(1000, long.MaxValue).Should().Be(1000);
        }

        [SkippableFact(typeof(MissingSftpCredentialsException))]
        public void ShouldThrowWhenMaxSequenceReached()
        {
            Func<long, byte[]> GetValue = i => Encoding.ASCII.GetBytes($"The quick brown fox-{i}");

            var keyEncryptionKey = new KeyEncryptionKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
            var addressKey = new HmacKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
            var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, addressKey, Compressor.None);
            
            var keyNamespace = "asdf";
            var storage = new InMemoryChunkStorage();
            var dictionaryStorage = new VersionedDictionary(keyNamespace, storage, chunkEncryptor);
            var lookupKeyBytes = Encoding.ASCII.GetBytes("lookupkey");

            // Should be exactly one 'exists' call on the storage layer if this key does not exist
            var i = long.MaxValue;
            dictionaryStorage.TryPutValue(lookupKeyBytes, i, GetValue(i)).Should().BeTrue();

            // Should be exactly two 'exists' calls when the actual highest used sequence number is given as a hint
            Log.WriteLine(0, "hint is optimal");
            storage.ResetStats();
            var hint = long.MaxValue; // highest existing sequence number

            new Action(() => dictionaryStorage.GetNextSequenceNumber(lookupKeyBytes, hint)).Should().Throw<Exception>();
            storage.TotalExistsCalls.Should().Be(2);
        }
    }
}
