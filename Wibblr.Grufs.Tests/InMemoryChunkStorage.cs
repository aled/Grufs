using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Tests
{
    public class InMemoryChunkStorage : IChunkStorage
    {
        private Dictionary<Address, EncryptedChunk> _dict = new Dictionary<Address, EncryptedChunk>();

        public int TotalPutCalls { get; private set; }
        public int TotalExistsCalls { get; private set; }

        public void ResetStats()
        {
            TotalPutCalls = 0;
            TotalExistsCalls = 0;
        }

        public Task InitAsync(CancellationToken token)
        {
            // no op
            return Task.CompletedTask;
        }

        public Task<EncryptedChunk?> GetAsync(Address address, CancellationToken token)
        {
            EncryptedChunk? chunk = null;

            if (_dict.ContainsKey(address))
            {
                chunk =_dict[address];
            }

            return Task.FromResult(chunk);
        }

        public Task<PutStatus> PutAsync(EncryptedChunk chunk, OverwriteStrategy overwrite, CancellationToken token)
        {
            TotalPutCalls++;

            if (_dict.ContainsKey(chunk.Address))
            {
                switch (overwrite)
                {
                    case OverwriteStrategy.Allow:
                        // fall through;
                        break;

                    case OverwriteStrategy.Deny:
                        return Task.FromResult(PutStatus.OverwriteDenied);
                }
            }

            _dict[chunk.Address] = chunk;
            return Task.FromResult(PutStatus.Success);
        }

        public Task<bool> ExistsAsync(Address address, CancellationToken token)
        {
            TotalExistsCalls++;
            return Task.FromResult(_dict.ContainsKey(address));
        }

        public Task<long> CountAsync(CancellationToken token)
        {
            return Task.FromResult((long)_dict.Count());
        }

        public IAsyncEnumerable<Address> ListAddressesAsync(CancellationToken token)
        {
            return _dict.Keys.ToAsyncEnumerable();
        }

        public float DeduplicationCompressionRatio() =>
            TotalPutCalls == 0 ? 1f : _dict.Count / (float)TotalPutCalls; 
        
        public void Flush()
        {
            // no op
        }
    }
}
