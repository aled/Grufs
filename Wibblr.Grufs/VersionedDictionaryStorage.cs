using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs
{
    /// <summary>
    /// Versioning is done by including the version number int the lookup key. Ensure that it is not possible to end up with duplicate keys.
    /// </summary>
    public class VersionedDictionaryStorage
    {
        private IChunkStorage _chunkStorage;
        private static readonly byte[] magic = new byte[] { 0xd9, 0x00, 0xa4, 0xc7 }; // ensure that a nonversioned lookup key cannot be constructed to be the same as a versioned lookup key
        private static readonly byte serializationVersion = 0;

        public VersionedDictionaryStorage(IChunkStorage chunkStorage)
        {
            _chunkStorage = chunkStorage;
        }

        public Span<byte> GenerateStructuredLookupKey(ReadOnlySpan<byte> lookupKey, long sequenceNumber)
        {
            return new Buffer(1 + 4 + 4 + lookupKey.Length + 16)
                .Append(serializationVersion)
                .Append(magic)
                .Append(lookupKey.Length)
                .Append(lookupKey)
                .Append(sequenceNumber)
                .ToSpan();
        }

        public bool TryPutValue(KeyEncryptionKey contentKeyEncryptionKey, HmacKey addressKey, ReadOnlySpan<byte> lookupKey, long sequenceNumber, ReadOnlySpan<byte> value)
        {
            var encryptedValue = new ChunkEncryptor().EncryptBytes(InitializationVector.Random(), EncryptionKey.Random(), contentKeyEncryptionKey, value);
            var structuredLookupKey = GenerateStructuredLookupKey(lookupKey, sequenceNumber);
            var address = new Address(new Hmac(addressKey, structuredLookupKey));

            return _chunkStorage.TryPut(new EncryptedChunk(address, encryptedValue), OverwriteStrategy.DenyWithError);
        }

        public bool TryGetValue(KeyEncryptionKey contentKeyEncryptionKey, HmacKey addressKey, ReadOnlySpan<byte> lookupKey, long sequenceNumber, out ReadOnlySpan<byte> value)
        {
            var structuredLookupKey = GenerateStructuredLookupKey(lookupKey, sequenceNumber);
            var hmac = new Hmac(addressKey, structuredLookupKey);

            if (!_chunkStorage.TryGet(new Address(hmac), out var chunk))
            {
                value = null;
                return false;
            }

            value = new ChunkEncryptor().DecryptBytes(chunk.Content, contentKeyEncryptionKey).ToSpan();
            return true;
        }

        public long GetNextSequenceNumber(HmacKey addressKey, ReadOnlySpan<byte> lookupKey)
        {
            var lookupKeyArray = lookupKey.ToArray();
            Func<long, Address> GetAddress = sequenceNumber => new Address(new Hmac(addressKey, GenerateStructuredLookupKey(lookupKeyArray, sequenceNumber)));

            // Query repeatedly in a kind of binary search to see which versions exist.
            if (!_chunkStorage.Exists(GetAddress(0)))
            {
                return 0;
            }

            long highestExisting = 0;

            // Start by querying versions 1, 16, 256, etc to give an initial upper bound
            var candidate = highestExisting + 1;
            while (_chunkStorage.Exists(GetAddress(candidate)))
            {
                highestExisting = candidate;

                if (candidate == long.MaxValue)
                {
                    throw new Exception("Max sequence number reached");
                }
                else if (candidate > long.MaxValue / 4)
                {
                    candidate = long.MaxValue;
                }
                else
                {
                    candidate = candidate * 4;
                }
            }

            var lowestMissing = candidate;

            while (lowestMissing - highestExisting > 1) 
            {
                candidate = highestExisting + ((lowestMissing - highestExisting) / 2);

                if (_chunkStorage.Exists(GetAddress(candidate)))
                {
                    highestExisting = candidate;
                }
                else
                {
                    lowestMissing = candidate;
                }
            }

            return lowestMissing;
        }
    }
}
