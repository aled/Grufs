using System;

namespace Wibblr.Grufs.Tests
{
    internal class InMemoryChunkStorage : IChunkStorage
    {
        private Dictionary<Address, EncryptedChunk> _dict = new Dictionary<Address, EncryptedChunk>();

        public int TotalPutCalls = 0;
        public int TotalExistsCalls = 0;

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

        public bool TryPut(EncryptedChunk chunk, OverwriteStrategy overwrite)
        {
            TotalPutCalls++;

            if (_dict.ContainsKey(chunk.Address))
            {
                switch (overwrite)
                {
                    case OverwriteStrategy.Allow:
                        break;

                    case OverwriteStrategy.DenyWithError:
                        return false;

                    case OverwriteStrategy.DenyWithSuccess:
                        return true;

                    case OverwriteStrategy.VerifyChecksum:
                        throw new NotImplementedException();
                }
            }

            _dict[chunk.Address] = chunk;
            return true;
        }

        public bool Exists(Address address)
        {
            TotalExistsCalls++;
            return _dict.ContainsKey(address);
        }

        public int Count()
        {
            return _dict.Count();
        }

        public IEnumerable<Address> ListAddresses()
        {
            return _dict.Keys.ToArray();
        }

        public float DeduplicationRatio => TotalPutCalls == 0 ? 100f : (_dict.Count * 100f) / TotalPutCalls;
    }
}
    