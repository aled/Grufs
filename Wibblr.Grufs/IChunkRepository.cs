using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs
{
    public interface IChunkRepository
    {
        public bool TryPut(EncryptedChunk chunk);
        public bool TryGet(Address address, out EncryptedChunk chunk);
    }
}