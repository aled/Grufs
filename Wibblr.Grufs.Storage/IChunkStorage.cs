
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
        public Task InitAsync(CancellationToken token);
        public Task<PutStatus> PutAsync(EncryptedChunk chunk, OverwriteStrategy overwriteStrategy, CancellationToken token);
        public Task<EncryptedChunk?> GetAsync(Address address, CancellationToken token);
        public Task<long> CountAsync(CancellationToken token);
        public Task<bool> ExistsAsync(Address address, CancellationToken token);
        public IAsyncEnumerable<Address> ListAddressesAsync(CancellationToken token);
        public void Flush();
    }
}
