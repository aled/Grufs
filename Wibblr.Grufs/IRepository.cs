
namespace Wibblr.Grufs
{
    public interface IRepository
    {
        public bool TryPut(EncryptedChunk chunk);
        public bool TryGet(Address address, out EncryptedChunk chunk);
    }
}