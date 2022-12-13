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

        public bool TryPut(EncryptedChunk chunk, OverwriteStrategy overwrite)
        {
            totalPuts++;

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

        public float DeduplicationRatio => totalPuts == 0 ? 100f : (_dict.Count * 100f) / totalPuts;
    }
}
    