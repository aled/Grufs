namespace Wibblr.Grufs
{
    public interface IChunkStorage
    {
        public bool TryPut(EncryptedChunk chunk, OverwriteStrategy overwriteStrategy);
        public bool TryGet(Address address, out EncryptedChunk chunk);
        public bool Exists(Address address);
        public IEnumerable<Address> ListAddresses();
    }
}
