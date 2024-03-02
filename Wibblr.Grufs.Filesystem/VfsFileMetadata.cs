using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Wibblr.Grufs.Core;
using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Filesystem
{
    [DebuggerDisplay("{ToString()}")]
    public record struct VfsFileMetadata 
    {
        public required Filename Name { get; init; }

        public required Address Address { get; init; }

        public required byte IndexLevel { get; init; }

        public required Timestamp VfsLastModified { get; init; }

        public required Timestamp LastModifiedTimestamp { get; init; }

        public required long Size { get; init; }

        [SetsRequiredMembers]
        public VfsFileMetadata(Filename name, Address address, byte indexLevel, Timestamp vfsLastModified, Timestamp lastModifiedTimestamp, long size)
        {
            Name = name;
            Address = address;
            IndexLevel = indexLevel;
            VfsLastModified = vfsLastModified;
            LastModifiedTimestamp = lastModifiedTimestamp;
            Size = size;
        }

        [SetsRequiredMembers]
        public VfsFileMetadata(BufferReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);

            var serializationVersion = reader.ReadByte();
            switch (serializationVersion)
            {
                case 0:
                    {
                        Name = new Filename(reader);
                        Address = reader.ReadAddress();
                        IndexLevel = reader.ReadByte();
                        VfsLastModified = reader.ReadTimestamp();
                        LastModifiedTimestamp = reader.ReadTimestamp();
                        Size = reader.ReadLong();
                    }
                    break;

                default:
                    throw new Exception("Invalid serialization version");
            }
        }

        public int GetSerializedLength() =>
            1 + // serialization version
            Name.GetSerializedLength() +
            Address.Length +
            1 + // chunk type
            VfsLastModified.GetSerializedLength() +
            LastModifiedTimestamp.GetSerializedLength() +
            new VarLong(Size).GetSerializedLength();

        public void SerializeTo(BufferBuilder builder)
        {
            builder.AppendByte(0); // serialization version
            Name.SerializeTo(builder);
            builder.AppendAddress(Address);
            builder.AppendByte(IndexLevel);
            builder.AppendTimestamp(VfsLastModified);
            builder.AppendTimestamp(LastModifiedTimestamp);
            builder.AppendLong(Size);
        }

        public override string ToString()
        {
            return $"{Name.OriginalName} {Address.ToString().Substring(0, 7)}";
        }
    }
}
