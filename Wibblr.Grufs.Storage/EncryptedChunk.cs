using System;

namespace Wibblr.Grufs.Storage
{

    public struct EncryptedChunk
    {
        public Address Address { get; private init; }

        public byte[] Content { get; private init; }

        public EncryptedChunk(Address address, byte[] content)
        {
            Address = address;
            Content = content;
        }
    }
}
