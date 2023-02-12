using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Wibblr.Grufs.Core;
using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Filesystem
{
    [DebuggerDisplay("{ToString()}")]
    public record struct FileMetadata 
    {
        public required Filename Name { get; init; }

        public required Address Address { get; init; }

        public required byte IndexLevel { get; init; }

        public required Timestamp LastModifiedTimestamp { get; init; }

        [SetsRequiredMembers]
        public FileMetadata(Filename name, Address address, byte indexLevel, Timestamp lastModifiedTimestamp)
        {
            Name = name;
            Address = address;
            IndexLevel = indexLevel;
            LastModifiedTimestamp = lastModifiedTimestamp;
        }

        [SetsRequiredMembers]
        public FileMetadata(BufferReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);

            Name = new Filename(reader);
            Address = new Address(reader.ReadBytes(Address.Length));
            IndexLevel = reader.ReadByte();
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
            builder.AppendByte(IndexLevel);
            builder.AppendTimestamp(LastModifiedTimestamp);
        }

        public override string ToString()
        {
            return $"{Name.OriginalName} {Address.ToString().Substring(0, 7)}";
        }
    }
}
