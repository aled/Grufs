using System.Collections.Immutable;
using System.Reflection;
using System.Text;

using Newtonsoft.Json.Linq;

using Shouldly;

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

    public class SequenceNumberTestsFixture<T> : IAsyncLifetime, IDisposable where T : IChunkStorageFactory, new()
    {
        private static CancellationToken token = CancellationToken.None;

        private T temporaryStorage = new();
        private IChunkStorage _storage;
        private VersionedDictionary _dictionary;
        private KeyEncryptionKey _keyEncryptionKey = new KeyEncryptionKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
        private HmacKey _addressKey = new HmacKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
        private string _keyNamespace = nameof(SequenceNumberTests<T>);

        public SequenceNumberTestsFixture()
        {
            _storage = temporaryStorage.GetChunkStorage();
            var chunkEncryptor = new ChunkEncryptor(_keyEncryptionKey, _addressKey, Compressor.None);
            _dictionary = new VersionedDictionary(_keyNamespace, _storage, chunkEncryptor);
        }

        public async Task InitializeAsync()
        {
            var token = CancellationToken.None;
            await _storage.InitAsync(token);

            // Add a bunch of values into the dictionary storage. For each of these IDs, insert that many versions of the value
            foreach (var lookupKeyId in new[] { 0, 1, 2, 10, 100, 1000 })
            {
                var start = DateTime.Now;
                for (long sequence = 0; sequence < lookupKeyId; sequence++)
                {
                    var lookupKey = Encoding.ASCII.GetBytes($"lookupkey-{lookupKeyId}");
                    var value = new ArrayBuffer(Encoding.ASCII.GetBytes($"value-{lookupKeyId}-sequence-{sequence}"));
                    (await _dictionary.PutValueAsync(lookupKey, sequence, value, token)).ShouldBeTrue();
                }
                Log.WriteLine(0, $"Created test fixture for lookup key id {lookupKeyId} in {((DateTime.Now.Ticks - start.Ticks) / 10000)} ms");
            }
        }

        public async Task<long> GetNextSequenceNumber(long lookupKeyId, long sequenceNumberHint)
        {
            var lookupKey = Encoding.ASCII.GetBytes($"lookupkey-{lookupKeyId}");
            var sequenceNumber = await _dictionary.GetNextSequenceNumberAsync(lookupKey, sequenceNumberHint, CancellationToken.None);
            return sequenceNumber;
        }

        public async Task<(long, int)> GetNextSequenceNumberAndLookupCount(long lookupKeyId, long sequenceNumberHint)
        {
            var lookupKey = Encoding.ASCII.GetBytes($"lookupkey-{lookupKeyId}");
            var lookupCounter = new VersionedDictionary.Counter();
            var sequenceNumber = await _dictionary.GetNextSequenceNumberAsync(lookupKey, sequenceNumberHint, lookupCounter, token);
            return (sequenceNumber, lookupCounter.Value);
        }

        public void Dispose()
        {
            temporaryStorage.Dispose();
        }

        public Task DisposeAsync()
        {
            throw new NotImplementedException();
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

        [Fact]
        public async Task ShouldReturnSequenceNumberZeroForMissingKey()
        {
            (await _fixture.GetNextSequenceNumber(0, 0)).ShouldBe(0);
            (await _fixture.GetNextSequenceNumber(0, 1)).ShouldBe(0);
            (await _fixture.GetNextSequenceNumber(0, 2)).ShouldBe(0);
            (await _fixture.GetNextSequenceNumber(0, long.MaxValue)).ShouldBe(0);
            (await _fixture.GetNextSequenceNumber(0, long.MinValue)).ShouldBe(0);
        }

        [Fact]
        public async Task ShouldHaveTwoLookupsWhenHintIsHighestExisting()
        {
            (await _fixture.GetNextSequenceNumberAndLookupCount(1, 0)).ShouldBe((1, 2));
            (await _fixture.GetNextSequenceNumberAndLookupCount(2, 1)).ShouldBe((2, 2));
            (await _fixture.GetNextSequenceNumberAndLookupCount(10, 9)).ShouldBe((10, 2));
            (await _fixture.GetNextSequenceNumberAndLookupCount(100, 99)).ShouldBe((100, 2));
            (await _fixture.GetNextSequenceNumberAndLookupCount(1000, 999)).ShouldBe((1000, 2));
        }

        [Fact]
        public async Task ShouldHaveThreeLookupsWhenHintIsOneLessThanHighestExisting()
        {
            (await _fixture.GetNextSequenceNumberAndLookupCount(2, 0)).ShouldBe((2, 3));
            (await _fixture.GetNextSequenceNumberAndLookupCount(10, 8)).ShouldBe((10, 3));
            (await _fixture.GetNextSequenceNumberAndLookupCount(100, 98)).ShouldBe((100, 3));
            (await _fixture.GetNextSequenceNumberAndLookupCount(1000, 998)).ShouldBe((1000, 3));
        }

        [Fact]
        public async Task ShouldHaveFiveLookupsWhenHintIsTwoLessThanHighestExisting()
        {
            (await _fixture.GetNextSequenceNumberAndLookupCount(10, 7)).ShouldBe((10, 5));
            (await _fixture.GetNextSequenceNumberAndLookupCount(100, 97)).ShouldBe((100, 5));
            (await _fixture.GetNextSequenceNumberAndLookupCount(1000, 997)).ShouldBe((1000, 5));
        }

        [Fact]
        public async Task ShouldHaveFiveLookupsWhenHintIsThreeLessThanHighestExisting()
        {
            (await _fixture.GetNextSequenceNumberAndLookupCount(10, 6)).ShouldBe((10, 5));
            (await _fixture.GetNextSequenceNumberAndLookupCount(100, 96)).ShouldBe((100, 5));
            (await _fixture.GetNextSequenceNumberAndLookupCount(1000, 996)).ShouldBe((1000, 5));
        }

        [Fact]
        public async Task ShouldWorkWhenHintIsTooHigh()
        {
            (await _fixture.GetNextSequenceNumber(0, 1)).ShouldBe(0);
            (await _fixture.GetNextSequenceNumber(0, 2)).ShouldBe(0);
            (await _fixture.GetNextSequenceNumber(0, 3)).ShouldBe(0);
            (await _fixture.GetNextSequenceNumber(0, long.MaxValue)).ShouldBe(0);

            (await _fixture.GetNextSequenceNumber(1, 2)).ShouldBe(1);
            (await _fixture.GetNextSequenceNumber(1, 3)).ShouldBe(1);
            (await _fixture.GetNextSequenceNumber(1, 4)).ShouldBe(1);
            (await _fixture.GetNextSequenceNumber(1, long.MaxValue)).ShouldBe(1);

            (await _fixture.GetNextSequenceNumber(2, 3)).ShouldBe(2);
            (await _fixture.GetNextSequenceNumber(2, 4)).ShouldBe(2);
            (await _fixture.GetNextSequenceNumber(2, 5)).ShouldBe(2);
            (await _fixture.GetNextSequenceNumber(2, long.MaxValue)).ShouldBe(2);

            (await _fixture.GetNextSequenceNumber(10, 11)).ShouldBe(10);
            (await _fixture.GetNextSequenceNumber(10, 12)).ShouldBe(10);
            (await _fixture.GetNextSequenceNumber(10, 13)).ShouldBe(10);
            (await _fixture.GetNextSequenceNumber(10, long.MaxValue)).ShouldBe(10);

            (await _fixture.GetNextSequenceNumber(100, 101)).ShouldBe(100);
            (await _fixture.GetNextSequenceNumber(100, 102)).ShouldBe(100);
            (await _fixture.GetNextSequenceNumber(100, 103)).ShouldBe(100);
            (await _fixture.GetNextSequenceNumber(100, long.MaxValue)).ShouldBe(100);

            (await _fixture.GetNextSequenceNumber(1000, 1001)).ShouldBe(1000);
            (await _fixture.GetNextSequenceNumber(1000, 1002)).ShouldBe(1000);
            (await _fixture.GetNextSequenceNumber(1000, 1003)).ShouldBe(1000);
            (await _fixture.GetNextSequenceNumber(1000, long.MaxValue)).ShouldBe(1000);
        }

        [Fact]
        public async Task ShouldThrowWhenMaxSequenceReached()
        {
            Func<long, ArrayBuffer> GetValue = i => new ArrayBuffer(Encoding.ASCII.GetBytes($"The quick brown fox-{i}"));

            var keyEncryptionKey = new KeyEncryptionKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
            var addressKey = new HmacKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
            var chunkEncryptor = new ChunkEncryptor(keyEncryptionKey, addressKey, Compressor.None);
            
            var keyNamespace = "asdf";
            var storage = new InMemoryChunkStorage();
            var dictionaryStorage = new VersionedDictionary(keyNamespace, storage, chunkEncryptor);
            var lookupKeyBytes = Encoding.ASCII.GetBytes("lookupkey");

            // Should be exactly one 'exists' call on the storage layer if this key does not exist
            var i = long.MaxValue;
            (await dictionaryStorage.PutValueAsync(lookupKeyBytes, i, GetValue(i), CancellationToken.None)).ShouldBeTrue();

            // Should be exactly two 'exists' calls when the actual highest used sequence number is given as a hint
            Log.WriteLine(0, "hint is optimal");
            storage.ResetStats();
            var hint = long.MaxValue; // highest existing sequence number

            Should.Throw<Exception>(async () => await dictionaryStorage.GetNextSequenceNumberAsync(lookupKeyBytes, hint, CancellationToken.None));
            storage.TotalExistsCalls.ShouldBe(2);
        }
    }
}
