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

        public bool TryGet(Address address, out EncryptedChunk chunk)
        {
            if (_dict.ContainsKey(address))
            {
                chunk = _dict[address];
                return true;
            }

            chunk = default;
            return false;
        }

        public PutStatus Put(EncryptedChunk chunk, OverwriteStrategy overwrite)
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
                        return PutStatus.OverwriteDenied;
                }
            }

            _dict[chunk.Address] = chunk;
            return PutStatus.Success;
        }

        public bool Exists(Address address)
        {
            TotalExistsCalls++;
            return _dict.ContainsKey(address);
        }

        public long Count()
        {
            return _dict.Count();
        }

        public IEnumerable<Address> ListAddresses()
        {
            return _dict.Keys.ToArray();
        }

        public float DeduplicationCompressionRatio() =>
            TotalPutCalls == 0 ? 1f : _dict.Count / (float)TotalPutCalls;
    }
}
    