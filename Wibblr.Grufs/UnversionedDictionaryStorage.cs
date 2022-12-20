using System;

using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs
{
    public class UnversionedDictionaryStorage
    {
        private IChunkStorage _chunkStorage;
        private static readonly byte serializationVersion = 0;
        private static readonly byte isVersioned = 0; // different value from the VersionedDictionaryStorage

        public UnversionedDictionaryStorage(IChunkStorage chunkStorage)
        {
            _chunkStorage = chunkStorage;
        }

        private ReadOnlySpan<byte> GenerateStructuredLookupKey(ReadOnlySpan<byte> lookupKey)
        {
            return new BufferBuilder(1 + 1 + 4 + 4 + lookupKey.Length)
                .AppendByte(serializationVersion)
                .AppendByte(isVersioned)
                .AppendInt(lookupKey.Length)
                .AppendBytes(lookupKey)
                .ToSpan();
        }

        public bool TryPutValue(KeyEncryptionKey contentKeyEncryptionKey, HmacKey addressKey, ReadOnlySpan<byte> lookupKey, ReadOnlySpan<byte> value, OverwriteStrategy overwrite)
        {
            var encryptedValue = new ChunkEncryptor().EncryptBytes(InitializationVector.Random(), EncryptionKey.Random(), contentKeyEncryptionKey, value);
            var structuredLookupKey = GenerateStructuredLookupKey(lookupKey);
            var hmac = new Hmac(addressKey, structuredLookupKey);
            var address = new Address(hmac);
            var encryptedChunk = new EncryptedChunk(address, encryptedValue);

            return _chunkStorage.TryPut(encryptedChunk, overwrite);
        }

        public bool TryGetValue(KeyEncryptionKey contentKeyEncryptionKey, HmacKey addressKey, ReadOnlySpan<byte> lookupKey, out ReadOnlySpan<byte> value)
        {
            var structuredLookupKey = GenerateStructuredLookupKey(lookupKey);
            var hmac = new Hmac(addressKey, structuredLookupKey);
            var address = new Address(hmac);

            if (!_chunkStorage.TryGet(address, out var chunk))
            {
                value = null;
                return false;
            }

            value = new ChunkEncryptor().DecryptBytes(chunk.Content, contentKeyEncryptionKey).AsSpan();
            return true;
        }
    }
}
