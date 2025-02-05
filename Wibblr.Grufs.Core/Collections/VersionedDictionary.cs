using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Core
{
    /// <summary>
    /// Versioning is done by including the version number int the lookup key. Ensure that it is not possible to end up with duplicate keys.
    /// </summary>
    public class VersionedDictionary
    {
        private readonly byte[] _keyNamespace;
        private readonly IChunkStorage _chunkStorage;
        private readonly ChunkEncryptor _chunkEncryptor;

        private static readonly byte _serializationVersion = 0;

        public VersionedDictionary(string keyNamespace, IChunkStorage chunkStorage, ChunkEncryptor chunkEncryptor)
        {
            _keyNamespace = new BufferBuilder(keyNamespace.GetSerializedLength()).AppendString(keyNamespace).GetUnderlyingArray();
            _chunkStorage = chunkStorage;
            _chunkEncryptor = chunkEncryptor;
        }

        private ArrayBuffer GenerateStructuredLookupKey(ReadOnlySpan<byte> lookupKey, long sequenceNumber)
        {
            var structuredLookupKeyLength =
                1 + // serialization version
                _keyNamespace.GetSerializedLength() +
                lookupKey.GetSerializedLength() +
                sequenceNumber.GetSerializedLength();

            return new BufferBuilder(structuredLookupKeyLength)
                .AppendByte(_serializationVersion)
                .AppendSpan(_keyNamespace)
                .AppendSpan(lookupKey)
                .AppendLong(sequenceNumber)
                .ToBuffer();
        }

        public async Task<bool> PutValueAsync(byte[] lookupKey, long sequenceNumber, ArrayBuffer value, CancellationToken token)
        {
            var structuredLookupKey = GenerateStructuredLookupKey(lookupKey.AsSpan(), sequenceNumber);
            var encryptedChunk = _chunkEncryptor.EncryptKeyAddressedChunk(structuredLookupKey.AsSpan(), value.AsSpan());

            return await _chunkStorage.PutAsync(encryptedChunk, OverwriteStrategy.Deny, token) == PutStatus.Success;
        }

        public async Task<ArrayBuffer> GetValueAsync(byte[] lookupKey, long sequenceNumber, CancellationToken token)
        {
            var structuredLookupKey = GenerateStructuredLookupKey(lookupKey.AsSpan(), sequenceNumber);
            var address =_chunkEncryptor.GetLookupKeyAddress(structuredLookupKey.AsSpan());

            if (await _chunkStorage.GetAsync(address, token) is EncryptedChunk c)
            {
                return _chunkEncryptor.DecryptBytes(c.Content);
            }

            return ArrayBuffer.Empty;
        }

        public async IAsyncEnumerable<(long sequenceNumber, ArrayBuffer value)> ValuesAsync(byte[] lookupKey, [EnumeratorCancellation] CancellationToken token)
        {
            long i = 0;

            ArrayBuffer buffer;
            while ((buffer = await GetValueAsync(lookupKey, i, token)) != ArrayBuffer.Empty)
            {
                yield return (i, buffer);
                i++;
            }
        }

        private async Task<bool> SequenceNumberExistsAsync(long sequenceNumber, byte[] lookupKey, Counter? lookupCounter, CancellationToken token)
        {
            var structuredLookupKey = GenerateStructuredLookupKey(lookupKey.AsSpan(), sequenceNumber);
            var address = _chunkEncryptor.GetLookupKeyAddress(structuredLookupKey.AsSpan());
            var exists = await _chunkStorage.ExistsAsync(address, token);
            lookupCounter?.Increment();
            //Log.WriteLine(0, $"Searching for seq# {sequenceNumber} - {(exists ? "found" : "missing")}");
            return exists;
        }

        public class Counter
        {
            private int _i;

            public void Increment() => Interlocked.Increment(ref _i);

            public int Value => _i;
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
        public Task<long> GetNextSequenceNumberAsync(byte[] lookupKey, long hintSequenceNumber, CancellationToken token)
        {
            return GetNextSequenceNumberAsync(lookupKey, hintSequenceNumber, null, token);
        }

        public async Task<long> GetNextSequenceNumberAsync(byte[] lookupKey, long hintSequenceNumber, Counter? lookupCounter, CancellationToken token)
        {
            // Query repeatedly in a kind of binary search to see which versions exist.
            if (hintSequenceNumber < 0)
            {
                hintSequenceNumber = 0;
            }

            // Caller has hinted that some sequence number exists. Verify if true or not.
            long highestExisting = hintSequenceNumber;
            long lowestMissing = long.MaxValue;
            while (!await SequenceNumberExistsAsync(highestExisting, lookupKey, lookupCounter, token))
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
                lowestMissing = (long)Math.Min((ulong)originalHighestExisting + (ulong)increment, long.MaxValue); // this cannot overflow as originalHighestExisting and increment are signed

                while (await SequenceNumberExistsAsync(lowestMissing, lookupKey, lookupCounter, token))
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

                //Log.WriteLine(0, $"Searching for sequence higher than {highestExisting} and less-than-or-equal to {lowestMissing}; trying {candidate}");

                if (await SequenceNumberExistsAsync(candidate, lookupKey, lookupCounter, token))
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
