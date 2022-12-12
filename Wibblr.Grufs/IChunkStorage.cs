namespace Wibblr.Grufs
{
    public interface IChunkStorage
    {
        public bool TryPut(EncryptedChunk chunk, bool allowOverwrite);
        public bool TryGet(Address address, out EncryptedChunk chunk);
    }
}