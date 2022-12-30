using System.Diagnostics;

namespace Wibblr.Grufs
{
    [DebuggerDisplay("{ToString()}")]
    public record struct RepositoryFile 
    {
        public RepositoryFilename Name { get; private init; }

        public Address Address { get; private init; }

        public ChunkType ChunkType { get; private init; }

        public Timestamp LastModifiedTimestamp { get; private init; }

        public RepositoryFile(RepositoryFilename name, Address address, ChunkType chunkType, Timestamp lastModifiedTimestamp)
        {
            Name = name;
            Address = address;
            ChunkType = chunkType;
            LastModifiedTimestamp = lastModifiedTimestamp;
        }
        
        public RepositoryFile(BufferReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);

            Name = reader.ReadRepositoryFilename();
            Address = new Address(reader.ReadBytes(Address.Length));
            ChunkType = (ChunkType)reader.ReadByte();
            LastModifiedTimestamp = reader.ReadTimestamp();
        }

        public int GetSerializedLength() => 
            Name.GetSerializedLength() + 
            Address.Length + 
            1 + // chunk type
            LastModifiedTimestamp.GetSerializedLength();

        public void SerializeTo(BufferBuilder builder)
        {
            builder.AppendRepositoryFilename(Name);
            builder.AppendBytes(Address);
            builder.AppendByte((byte)ChunkType);
            builder.AppendTimestamp(LastModifiedTimestamp);
        }

        public override string ToString()
        {
            return $"{Name.OriginalName} {Address.ToString().Substring(0, 7)}";
        }
    }
}
