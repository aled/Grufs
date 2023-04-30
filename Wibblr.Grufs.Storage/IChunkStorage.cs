namespace Wibblr.Grufs.Storage
{
    public enum PutStatus
    {
        Unknown = 0,
        Success = 1,
        OverwriteDenied = 2,
        Error = 3,
    }

    public interface IChunkStorage
    {
        public PutStatus Put(EncryptedChunk chunk, OverwriteStrategy overwriteStrategy);
        public bool TryGet(Address address, out EncryptedChunk chunk);
        public long Count();
        public bool Exists(Address address);
        public IEnumerable<Address> ListAddresses();
        public void Flush();
    }
}
