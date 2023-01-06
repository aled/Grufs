using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Wibblr.Grufs
{
    [DebuggerDisplay("{ToString()}")]
    public record struct FileMetadata 
    {
        public required Filename Name { get; init; }

        public required Address Address { get; init; }

        public required ChunkType ChunkType { get; init; }

        public required Timestamp LastModifiedTimestamp { get; init; }

        [SetsRequiredMembers]
        public FileMetadata(Filename name, Address address, ChunkType chunkType, Timestamp lastModifiedTimestamp)
        {
            Name = name;
            Address = address;
            ChunkType = chunkType;
            LastModifiedTimestamp = lastModifiedTimestamp;
        }

        [SetsRequiredMembers]
        public FileMetadata(BufferReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);

            Name = new Filename(reader);
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
            Name.SerializeTo(builder);
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
