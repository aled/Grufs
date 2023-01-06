using System.Text;

using FluentAssertions;

using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs.Tests
{
    public class SequenceNumberTestsFixture
    {
        private InMemoryChunkStorage _storage;
        private VersionedDictionaryStorage _dictionaryStorage;
        private KeyEncryptionKey keyEncryptionKey = new KeyEncryptionKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
        private HmacKey addressKey = new HmacKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
        private Compressor _compressor = new Compressor(CompressionAlgorithm.None);

        public SequenceNumberTestsFixture()
        {
            _storage = new InMemoryChunkStorage();
            _dictionaryStorage = new VersionedDictionaryStorage(keyEncryptionKey, addressKey, _compressor, _storage);

            // Add a bunch of values into the dictionary storage. For each of these IDs, insert that many versions of the value
            foreach (var lookupKeyId in new[] { 0, 1, 2, 10, 100, 1000 })
            {
                for (long sequence = 0; sequence < lookupKeyId; sequence++)
                {
                    var lookupKey = Encoding.ASCII.GetBytes($"lookupkey-{lookupKeyId}");
                    var value = Encoding.ASCII.GetBytes($"value-{lookupKeyId}-sequence-{sequence}");
                    _dictionaryStorage.TryPutValue(lookupKey, sequence, value).Should().BeTrue();
                }
            }
        }
        public long GetNextSequenceNumber(long lookupKeyId, long sequenceNumberHint)
        {
            var lookupKey = Encoding.ASCII.GetBytes($"lookupkey-{lookupKeyId}");
            var sequenceNumber = _dictionaryStorage.GetNextSequenceNumber(lookupKey, sequenceNumberHint);
            return sequenceNumber;
        }

        public (long sequenceNumber, int lookupCount) GetNextSequenceNumberAndLookupCount(long lookupKeyId, long sequenceNumberHint)
        {
            var lookupKey = Encoding.ASCII.GetBytes($"lookupkey-{lookupKeyId}");
            var sequenceNumber = _dictionaryStorage.GetNextSequenceNumber(lookupKey, sequenceNumberHint, out var lookupCount);
            return (sequenceNumber, lookupCount);
        }
    }

    public class SequenceNumberTests : IClassFixture<SequenceNumberTestsFixture>
    {
        private SequenceNumberTestsFixture _fixture;
        private Compressor _compressor = new Compressor(CompressionAlgorithm.None);

        public SequenceNumberTests(SequenceNumberTestsFixture fixture) 
        {
            _fixture = fixture;
        }

        [Fact]
        public void ShouldReturnSequenceNumberZeroForMissingKey()
        {
            _fixture.GetNextSequenceNumber(0, 0).Should().Be(0);
            _fixture.GetNextSequenceNumber(0, 1).Should().Be(0);
            _fixture.GetNextSequenceNumber(0, 2).Should().Be(0);
            _fixture.GetNextSequenceNumber(0, long.MaxValue).Should().Be(0);
            _fixture.GetNextSequenceNumber(0, long.MinValue).Should().Be(0);
        }

        [Fact]
        public void ShouldHaveTwoLookupsWhenHintIsHighestExisting()
        {
            _fixture.GetNextSequenceNumberAndLookupCount(1, 0).Should().Be((1, 2));
            _fixture.GetNextSequenceNumberAndLookupCount(2, 1).Should().Be((2, 2));
            _fixture.GetNextSequenceNumberAndLookupCount(10, 9).Should().Be((10, 2));
            _fixture.GetNextSequenceNumberAndLookupCount(100, 99).Should().Be((100, 2));
            _fixture.GetNextSequenceNumberAndLookupCount(1000, 999).Should().Be((1000, 2));
        }

        [Fact]
        public void ShouldHaveThreeLookupsWhenHintIsOneLessThanHighestExisting()
        {
            _fixture.GetNextSequenceNumberAndLookupCount(2, 0).Should().Be((2, 3));
            _fixture.GetNextSequenceNumberAndLookupCount(10, 8).Should().Be((10, 3));
            _fixture.GetNextSequenceNumberAndLookupCount(100, 98).Should().Be((100, 3));
            _fixture.GetNextSequenceNumberAndLookupCount(1000, 998).Should().Be((1000, 3));
        }

        [Fact]
        public void ShouldHaveFiveLookupsWhenHintIsTwoLessThanHighestExisting()
        {
            _fixture.GetNextSequenceNumberAndLookupCount(10, 7).Should().Be((10, 5));
            _fixture.GetNextSequenceNumberAndLookupCount(100, 97).Should().Be((100, 5));
            _fixture.GetNextSequenceNumberAndLookupCount(1000, 997).Should().Be((1000, 5));
        }

        [Fact]
        public void ShouldHaveFiveLookupsWhenHintIsThreeLessThanHighestExisting()
        {
            _fixture.GetNextSequenceNumberAndLookupCount(10, 6).Should().Be((10, 5));
            _fixture.GetNextSequenceNumberAndLookupCount(100, 96).Should().Be((100, 5));
            _fixture.GetNextSequenceNumberAndLookupCount(1000, 996).Should().Be((1000, 5));
        }

        [Fact]
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

        [Fact]
        public void ShouldThrowWhenMaxSequenceReached()
        {
            Func<long, byte[]> GetValue = i => Encoding.ASCII.GetBytes($"The quick brown fox-{i}");

            var keyEncryptionKey = new KeyEncryptionKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
            var addressKey = new HmacKey(Convert.FromHexString("0000000000000000000000000000000000000000000000000000000000000000"));
            var storage = new InMemoryChunkStorage();
            var dictionaryStorage = new VersionedDictionaryStorage(keyEncryptionKey, addressKey, _compressor, storage);
            var lookupKeyBytes = Encoding.ASCII.GetBytes("lookupkey");

            // Should be exactly one 'exists' call on the storage layer if this key does not exist
            var i = long.MaxValue;
            dictionaryStorage.TryPutValue(lookupKeyBytes, i, GetValue(i)).Should().BeTrue();

            // Should be exactly two 'exists' calls when the actual highest used sequence number is given as a hint
            Console.WriteLine("hint is optimal");
            storage.ResetStats();
            var hint = long.MaxValue; // highest existing sequence number

            new Action(() => dictionaryStorage.GetNextSequenceNumber(lookupKeyBytes, hint)).Should().Throw<Exception>();
            storage.TotalExistsCalls.Should().Be(2);
        }
    }
}
