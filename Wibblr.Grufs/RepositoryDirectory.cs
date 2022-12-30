using System;
using System.Collections.Immutable;

namespace Wibblr.Grufs
{
    /// <summary>
    /// A directory is stored in the repository using the versioned dictionary storage.
    /// 
    /// To find a subdirectory, find the directory with the appropriate name, and the latest version
    /// that has it's parent version set to this directory version.
    /// </summary>
    public sealed record RepositoryDirectory
    {
        public RepositoryDirectoryPath Path { get; private init; }  // set to 
        public long ParentVersion { get; private init; }
        public Timestamp LastModifiedTimestamp { get; private init; }
        public bool IsDeleted { get; private init; }
        public ImmutableArray<RepositoryFile> Files { get; set; }
        public ImmutableArray<RepositoryFilename> Directories { get; set; }
        
        public RepositoryDirectory(BufferReader reader)
        {
            Path = reader.ReadRepositoryDirectoryPath();
            ParentVersion = reader.ReadLong();
            LastModifiedTimestamp = reader.ReadTimestamp();
            IsDeleted = reader.ReadByte() != 0;

            // Limit number of files in a directory to 10000 to protect against invalid or malicious inputs
            // This is not a technical limitation so could be raised if needed.
            int fileCount = reader.ReadVarInt();
            if (fileCount > 10000)
            {
                throw new Exception("Too many files in directory - limit is 10000");
            }

            var filesBuilder = ImmutableArray.CreateBuilder<RepositoryFile>();
            for (int i = 0; i < fileCount; i++)
            {
                filesBuilder.Add(reader.ReadRepositoryFile());
            }
            Files = filesBuilder.OrderBy(x => x.Name.CanonicalName).ToImmutableArray();

            int directoryCount = reader.ReadVarInt();
            if (directoryCount > 10000)
            {
                throw new Exception("Too many subdirectories in directory - limit is 10000");
            }
            var directoriesBuilder = ImmutableArray.CreateBuilder<RepositoryFilename>();
            for (int i = 0; i < directoryCount; i++)
            {
                directoriesBuilder.Add(reader.ReadRepositoryFilename());
            }
            Directories = directoriesBuilder.OrderBy(x => x.CanonicalName).ToImmutableArray();
        }

        public RepositoryDirectory(RepositoryDirectoryPath path, long parentVersion, Timestamp lastModifiedTimestamp, bool isDeleted, IEnumerable<RepositoryFile> files, IEnumerable<RepositoryFilename> directories)
        {
            Path = path;
            ParentVersion = parentVersion;
            LastModifiedTimestamp = lastModifiedTimestamp;
            IsDeleted = isDeleted;
            Files = files.OrderBy(x => x.Name.CanonicalName).ToImmutableArray();
            Directories = directories.OrderBy(x => x.CanonicalName).ToImmutableArray();
        }

        public int GetSerializedLength() =>
            Path.GetSerializedLength() +
            8 + // ParentVersion
            LastModifiedTimestamp.GetSerializedLength() +
            1 + // IsDeleted
            new VarInt(Files.Count()).GetSerializedLength() +
            Files.Sum(x => x.GetSerializedLength()) +
            new VarInt(Directories.Count()).GetSerializedLength() +
            Directories.Sum(x => x.GetSerializedLength());

        public void SerializeTo(BufferBuilder builder)
        {
            builder.AppendRepositoryDirectoryPath(Path);
            builder.AppendLong(ParentVersion);
            builder.AppendTimestamp(LastModifiedTimestamp);
            builder.AppendByte((byte)(IsDeleted ? 1 : 0));
            builder.AppendVarInt(new VarInt(Files.Count()));
            foreach (var file in Files)
            {
                builder.AppendRepositoryFile(file);
            }
            builder.AppendVarInt(new VarInt(Directories.Count()));
            foreach (var directory in Directories)
            {
                builder.AppendRepositoryFilename(directory);
            }
        }

        public bool Equals(RepositoryDirectory? other)
        {
            return other != null &&
                Path == other.Path &&
                ParentVersion == other.ParentVersion &&
                LastModifiedTimestamp == other.LastModifiedTimestamp &&
                IsDeleted == other.IsDeleted &&
                Files.SequenceEqual(other.Files) &&
                Directories.SequenceEqual(other.Directories);
        }

        public override int GetHashCode()
        {
            int i = 0;

            foreach (var file in Files)
            {
                i = unchecked((i * 17) + Files[i].GetHashCode());
            }
            foreach (var directory in Directories)
            {
                i = unchecked((i * 17) + Files[i].GetHashCode());
            }
            return HashCode.Combine(Path, ParentVersion, LastModifiedTimestamp, IsDeleted, i);
        }
    }
}
