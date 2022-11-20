using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wibblr.Grufs.Tests
{
    internal class TestChunkRepository : IChunkRepository
    {
        private Dictionary<Address, EncryptedChunk> _dict = new Dictionary<Address, EncryptedChunk>();

        private int totalPuts = 0;

        public bool TryGet(Address address, out EncryptedChunk? chunk)
        {
            return _dict.TryGetValue(address, out chunk);
        }

        public bool TryPut(EncryptedChunk chunk)
        {
            totalPuts++;
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
