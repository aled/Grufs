using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wibblr.Grufs.Tests
{
    internal class TestRepository : IRepository
    {
        private Dictionary<Address, EncryptedChunk> _dict = new Dictionary<Address, EncryptedChunk>();

        public bool TryGet(Address address, out EncryptedChunk? chunk)
        {
            return _dict.TryGetValue(address, out chunk);
        }

        public bool TryPut(EncryptedChunk chunk)
        {
            _dict[chunk.Address] = chunk;
            return true;
        }

        public int Count()
        {
            return _dict.Count();
        }
    }
}
