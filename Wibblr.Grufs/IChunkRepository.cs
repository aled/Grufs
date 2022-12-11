using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs
{
    public interface IChunkRepository
    {
        public bool TryPut(EncryptedChunk chunk, bool allowOverwrite);
        public bool TryGet(Address address, out EncryptedChunk chunk);
    }
}