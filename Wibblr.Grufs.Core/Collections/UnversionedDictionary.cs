using System;

using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Core
{
    public class UnversionedDictionary
    {
        private readonly IChunkStorage _chunkStorage;
        private readonly ChunkEncryptor _chunkEncryptor;
      
        private static readonly byte serializationVersion = 0;
     
        public UnversionedDictionary(IChunkStorage chunkStorage, ChunkEncryptor chunkEncryptor)
        {
            _chunkStorage = chunkStorage;
            _chunkEncryptor = chunkEncryptor;
        }

        private ReadOnlySpan<byte> GenerateStructuredLookupKey(ReadOnlySpan<byte> lookupKey)
        {
            var lookupKeyLength = new VarInt(lookupKey.Length);

            var bufferLength =
                1 + // serialization version
                lookupKeyLength.GetSerializedLength() + // lookup key length
                lookupKey.Length; // lookup key

            return new BufferBuilder(bufferLength)
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

        public bool TryGetValue(ReadOnlySpan<byte> lookupKey, out ArrayBuffer value)
        {
            var structuredLookupKey = GenerateStructuredLookupKey(lookupKey);
            var address = _chunkEncryptor.GetLookupKeyAddress(structuredLookupKey);

            if (!_chunkStorage.TryGet(address, out var chunk))
            {
                value = ArrayBuffer.Empty;
                return false;
            }

            value = _chunkEncryptor.DecryptBytes(chunk.Content);
            return true;
        }
    }
}
