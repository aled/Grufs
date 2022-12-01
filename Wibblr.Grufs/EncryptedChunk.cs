using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs
{
    public class EncryptedChunk
    {
        public Address Address { get; init; }

        public byte[] Content { get; init; }

        public EncryptedChunk(Address address, byte[] content)
        {
            Address = address;
            Content = content;
        }
    }
}
