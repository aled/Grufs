using System;

namespace Wibblr.Grufs.Tests
{
    internal class InMemoryChunkStorage : IChunkStorage
    {
        private Dictionary<Address, EncryptedChunk> _dict = new Dictionary<Address, EncryptedChunk>();

        private int totalPuts = 0;

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

        public bool TryPut(EncryptedChunk chunk, bool allowOverwrite)
        {
            totalPuts++;

            if (_dict.ContainsKey(chunk.Address) && !allowOverwrite) 
            {
                return false;
            }

            _dict[chunk.Address] = chunk;

            return true;
        }

        public int Count()
        {
            return _dict.Count();
        }

        public float DeduplicationRatio => totalPuts == 0 ? 100f : (_dict.Count * 100f) / totalPuts;
    }
}
