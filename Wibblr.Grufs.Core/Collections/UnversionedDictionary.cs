using System;
using System.Collections.Immutable;

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

        private ArrayBuffer GenerateStructuredLookupKey(ReadOnlySpan<byte> lookupKey)
        {
            var bufferLength =
                1 + // serialization version
                lookupKey.GetSerializedLength(); // lookup key

            return new BufferBuilder(bufferLength)
                .AppendByte(serializationVersion)
                .AppendSpan(lookupKey)
                .ToBuffer();
        }

        public async Task<PutStatus> PutValueAsync(ImmutableArray<byte> lookupKey, ArrayBuffer value, OverwriteStrategy overwrite, CancellationToken token)
        {
            var structuredLookupKey = GenerateStructuredLookupKey(lookupKey.AsSpan());
            var encryptedChunk = _chunkEncryptor.EncryptKeyAddressedChunk(structuredLookupKey.AsSpan(), value.AsSpan());

            return await _chunkStorage.PutAsync(encryptedChunk, overwrite, token);
        }

        public async Task<ArrayBuffer> GetValueAsync(ImmutableArray<byte> lookupKey, CancellationToken token)
        {
            var structuredLookupKey = GenerateStructuredLookupKey(lookupKey.AsSpan());
            var address = _chunkEncryptor.GetLookupKeyAddress(structuredLookupKey.AsSpan());
            var chunk = await _chunkStorage.GetAsync(address, token);

            if (chunk is EncryptedChunk c)
            {
                return _chunkEncryptor.DecryptBytes(c.Content);
            }
            return ArrayBuffer.Empty;
        }
    }
}
