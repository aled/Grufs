using System;
using System.Text;

namespace Wibblr.Grufs
{
    /// <summary>
    /// A directory is stored in the repository using the versioned dictionary storage.
    /// </summary>
    internal class RepositoryDirectory
    {
        public PathString Name { get; set; }
        public Timestamp LastModifiedTimestamp { get; set; }
        public IList<RepositoryFile> Files { get; set; }
        public IList<PathString> Directories { get; set; }
        public bool IsDeleted { get; set; }

        public RepositoryDirectory(BufferReader reader)
        {
            Name = reader.ReadPathString();
            LastModifiedTimestamp = reader.ReadTimestamp();
        }

        public int SerializedLength() =>
            Name.GetSerializedLength() +
            LastModifiedTimestamp.GetSerializedLength() +
            new VarInt(Files.Count).GetSerializedLength() +
            Files.Sum(x => x.GetSerializedLength()) +
            new VarInt(Directories.Count).GetSerializedLength() +
            Directories.Sum(x => x.SerializedLength()) +
            1; // IsDeleted;

        public void CopyTo(BufferBuilder builder)
        {
            builder.AppendPathString(Name);
            builder.AppendTimestamp(LastModifiedTimestamp);
            builder.AppendVarInt(new VarInt(Files.Count));
            foreach (var file in Files)
            {
                builder.AppendFile(file);
            }
            builder.AppendVarInt(new VarInt(Directories.Count));
            foreach (var directory in Directories)
            {
                builder.AppendPathString(directory);
            }
            builder.AppendByte(IsDeleted ? 1 : 0);
        }
    }
}
