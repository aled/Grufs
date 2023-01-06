using System;
using System.IO.Compression;

using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs
{
    public class UnversionedDictionaryStorage
    {
        private KeyEncryptionKey _contentKeyEncryptionKey;
        private HmacKey _addressKey;
        private Compressor _compressor;
        private IChunkStorage _chunkStorage;
      
        private static readonly byte serializationVersion = 0;
     
        public UnversionedDictionaryStorage(KeyEncryptionKey contentKeyEncryptionKey, HmacKey addressKey, Compressor compressor, IChunkStorage chunkStorage)
        {
            _contentKeyEncryptionKey = contentKeyEncryptionKey;
            _addressKey = addressKey;
            _compressor = compressor;
            _chunkStorage = chunkStorage;
        }

        private ReadOnlySpan<byte> GenerateStructuredLookupKey(ReadOnlySpan<byte> lookupKey)
        {
            return new BufferBuilder(1 + 4 + 4 + lookupKey.Length)
                .AppendByte(serializationVersion)
                .AppendInt(lookupKey.Length)
                .AppendBytes(lookupKey)
                .ToSpan();
        }

        public bool TryPutValue(ReadOnlySpan<byte> lookupKey, ReadOnlySpan<byte> value, OverwriteStrategy overwrite)
        {
            var encryptedValue = new ChunkEncryptor(_contentKeyEncryptionKey, _addressKey, _compressor).EncryptBytes(InitializationVector.Random(), EncryptionKey.Random(), value);
            var structuredLookupKey = GenerateStructuredLookupKey(lookupKey);
            var hmac = new Hmac(_addressKey, structuredLookupKey);
            var address = new Address(hmac.ToSpan());
            var encryptedChunk = new EncryptedChunk(address, encryptedValue);

            return _chunkStorage.TryPut(encryptedChunk, overwrite);
        }

        public bool TryGetValue(ReadOnlySpan<byte> lookupKey, out Buffer value)
        {
            var structuredLookupKey = GenerateStructuredLookupKey(lookupKey);
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
    }
}
