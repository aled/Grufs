using System;

using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs
{
    /// <summary>
    /// Versioning is done by including the version number int the lookup key. Ensure that it is not possible to end up with duplicate keys.
    /// </summary>
    public class VersionedDictionaryStorage
    {
        private KeyEncryptionKey _contentKeyEncryptionKey;
        private HmacKey _addressKey;
        private Compressor _compressor;
        private IChunkStorage _chunkStorage;

        private static readonly byte serializationVersion = 0;

        public VersionedDictionaryStorage(KeyEncryptionKey contentKeyEncryptionKey, HmacKey addressKey, Compressor compressor, IChunkStorage chunkStorage)
        {
            _contentKeyEncryptionKey = contentKeyEncryptionKey;
            _addressKey = addressKey;
            _compressor = compressor;
            _chunkStorage = chunkStorage;
        }

        private ReadOnlySpan<byte> GenerateStructuredLookupKey(ReadOnlySpan<byte> lookupKey, long sequenceNumber)
        {
            return new BufferBuilder(1 + 4 + lookupKey.Length + 8)
                .AppendByte(serializationVersion)
                .AppendInt(lookupKey.Length)
                .AppendBytes(lookupKey)
                .AppendLong(sequenceNumber)
                .ToSpan();
        }   

        public bool TryPutValue(ReadOnlySpan<byte> lookupKey, long sequenceNumber, ReadOnlySpan<byte> value)
        {
            var structuredLookupKey = GenerateStructuredLookupKey(lookupKey, sequenceNumber);
            var hmac = new Hmac(_addressKey, structuredLookupKey);
            var address = new Address(hmac.ToSpan());
            var chunkEncryptor = new ChunkEncryptor(_contentKeyEncryptionKey, _addressKey, _compressor);
            var encryptedValue = chunkEncryptor.EncryptBytes(InitializationVector.Random(), EncryptionKey.Random(), value);
            var encryptedChunk = new EncryptedChunk(address, encryptedValue);

            return _chunkStorage.TryPut(encryptedChunk, OverwriteStrategy.DenyWithError);
        }

        public bool TryGetValue(ReadOnlySpan<byte> lookupKey, long sequenceNumber, out Buffer value)
        {
            var structuredLookupKey = GenerateStructuredLookupKey(lookupKey, sequenceNumber);
            var hmac = new Hmac(_addressKey, structuredLookupKey);
            var address = new Address(hmac.ToSpan());

            if (!_chunkStorage.TryGet(address, out var chunk))
            {
                value = Buffer.Empty;
                return false;
            }

            value = new ChunkEncryptor(_contentKeyEncryptionKey, _addressKey, _compressor).DecryptBytes(chunk.Content);
            return true;
        }

        private bool SequenceNumberExists(long sequenceNumber, ReadOnlySpan<byte> lookupKey, ref int lookupCount)
        {
            var structuredLookupKey = GenerateStructuredLookupKey(lookupKey, sequenceNumber);
            var hmac = new Hmac(_addressKey, structuredLookupKey);
            var address = new Address(hmac.ToSpan());
            var exists = _chunkStorage.Exists(address);
            lookupCount++;
            //Console.WriteLine($"Searching for seq# {sequenceNumber} - {(exists ? "found" : "missing")}");
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
        public long GetNextSequenceNumber(ReadOnlySpan<byte> lookupKey, long hintSequenceNumber)
        {
            return GetNextSequenceNumber(lookupKey, hintSequenceNumber, out _);
        }

        public long GetNextSequenceNumber(ReadOnlySpan<byte> lookupKey, long hintSequenceNumber, out int lookupCount)
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
            while (!SequenceNumberExists(highestExisting, lookupKey, ref lookupCount))
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

                while (SequenceNumberExists(lowestMissing, lookupKey, ref lookupCount))
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

                if (SequenceNumberExists(candidate, lookupKey, ref lookupCount))
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
