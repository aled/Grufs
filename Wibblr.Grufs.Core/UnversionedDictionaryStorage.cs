using System;

namespace Wibblr.Grufs
{
    public class UnversionedDictionaryStorage
    {
        private readonly IChunkStorage _chunkStorage;
        private readonly ChunkEncryptor _chunkEncryptor;
      
        private static readonly byte serializationVersion = 0;
     
        public UnversionedDictionaryStorage(IChunkStorage chunkStorage, ChunkEncryptor chunkEncryptor)
        {
            _chunkStorage = chunkStorage;
            _chunkEncryptor = chunkEncryptor;
        }

        private ReadOnlySpan<byte> GenerateStructuredLookupKey(ReadOnlySpan<byte> lookupKey)
        {
            return new BufferBuilder(1 + 4 + 4 + lookupKey.Length)
                .AppendByte(serializationVersion)
                .AppendInt(lookupKey.Length)
                .AppendBytes(lookupKey)
                .ToSpan();
        }

        public PutStatus TryPutValue(ReadOnlySpan<byte> lookupKey, ReadOnlySpan<byte> value, OverwriteStrategy overwrite)
        {
            var structuredLookupKey = GenerateStructuredLookupKey(lookupKey);
            var encryptedChunk = _chunkEncryptor.EncryptKeyAddressedChunk(structuredLookupKey, value);

            return _chunkStorage.Put(encryptedChunk, overwrite);
        }

        public bool TryGetValue(ReadOnlySpan<byte> lookupKey, out Buffer value)
        {
            var structuredLookupKey = GenerateStructuredLookupKey(lookupKey);
            var address = _chunkEncryptor.GetLookupKeyAddress(structuredLookupKey);

            if (!_chunkStorage.TryGet(address, out var chunk))
            {
                value = Buffer.Empty;
                return false;
            }

            value = _chunkEncryptor.DecryptBytes(chunk.Content);
            return true;
        }
    }
}
