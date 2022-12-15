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

        private bool SequenceNumberExists(long sequenceNumber, HmacKey addressKey, ReadOnlySpan<byte> lookupKey, ref int lookupCount)
        {
            var exists = _chunkStorage.Exists(new Address(new Hmac(addressKey, GenerateStructuredLookupKey(lookupKey, sequenceNumber))));
            lookupCount++;

            Console.WriteLine($"Searching for seq# {sequenceNumber} - {(exists ? "found" : "missing")}");

            return exists;
        }

        /// <summary>
        /// 
        /// This should work regardless of the hint sequence number, although it will only be optimal if the hint
        /// is the highest existing sequence number. In particular:
        /// 
        ///   o If the hint sequence number is 0, and there are no keys, then a single lookup will occur
        ///   o If the hint sequence number is the highest existing sequence number, then exactly two lookups will occur
        /// </summary>
        /// <param name="addressKey"></param>
        /// <param name="lookupKey"></param>
        /// <param name="hintSequenceNumber"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public long GetNextSequenceNumber(HmacKey addressKey, ReadOnlySpan<byte> lookupKey, long hintSequenceNumber)
        {
            return GetNextSequenceNumber(addressKey, lookupKey, hintSequenceNumber, out _);
        }

        public long GetNextSequenceNumber(HmacKey addressKey, ReadOnlySpan<byte> lookupKey, long hintSequenceNumber, out int lookupCount)
        {
            lookupCount = 0;

            // Query repeatedly in a kind of binary search to see which versions exist.
            if (hintSequenceNumber < 0)
            {
                hintSequenceNumber = 0;
            }

            // Caller has hinted that some sequence number exists. Verify if true or not.
            long highestExisting = hintSequenceNumber;
            long lowestMissing = long.MaxValue;
            while (!SequenceNumberExists(highestExisting, addressKey, lookupKey, ref lookupCount))
            {
                if (highestExisting == 0)
                {
                    return 0;
                }

                lowestMissing = highestExisting;
                highestExisting /= 2;
            }

            // Now we have a verified existing sequence number.
            // If the caller-asserted highest seen sequence number did exist,
            // start incrementing the highestExisting by 1, 2, 4, etc to get the lowestMissing.
            var originalHighestExisting = highestExisting;
            if (lowestMissing == long.MaxValue)
            {
                var increment = 1L;
                lowestMissing = (long)Math.Min((ulong)originalHighestExisting + (ulong)increment, long.MaxValue);

                while (SequenceNumberExists(lowestMissing, addressKey, lookupKey, ref lookupCount))
                {
                    if (lowestMissing == long.MaxValue)
                    {
                        throw new Exception("Max sequence number reached");
                    }
                    if (lowestMissing > highestExisting)
                    {
                        highestExisting = lowestMissing;
                    }

                    increment *= 2;
                    lowestMissing = (long)Math.Min((ulong)originalHighestExisting + (ulong)increment, long.MaxValue);
                }
            }

            while (lowestMissing - highestExisting > 1) 
            {
                var candidate = highestExisting + ((lowestMissing - highestExisting) / 2);

                //Console.WriteLine($"Searching for sequence higher than {highestExisting} and less-than-or-equal to {lowestMissing}; trying {candidate}");

                if (SequenceNumberExists(candidate, addressKey, lookupKey, ref lookupCount))
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
