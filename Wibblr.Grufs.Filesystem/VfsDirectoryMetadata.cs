using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Filesystem;

/// <summary>
/// A VFS directory is stored in the repository using the versioned dictionary storage.
/// 
/// To find a subdirectory, find the directory with the appropriate name, and the latest version
/// that has it's parent version set to this directory version.
/// </summary>
[DebuggerDisplay("{ToString()}")]
public record VfsDirectoryMetadata
{
    public required DirectoryPath Path { get; init; }  // set to 
    public required long ParentVersion { get; init; }
    public required Timestamp SnapshotTimestamp { get; init; }
    public required bool IsDeleted { get; init; }
    public required ImmutableArray<VfsFileMetadata> Files { get; init; }
    public required ImmutableArray<Filename> Directories { get; init; }

    [SetsRequiredMembers]
    public VfsDirectoryMetadata(DirectoryPath path, long parentVersion, Timestamp snapshotTimestamp, bool isDeleted, IEnumerable<VfsFileMetadata> files, IEnumerable<Filename> directories)
    {
        Path = path;
        ParentVersion = parentVersion;
        SnapshotTimestamp = snapshotTimestamp;
        IsDeleted = isDeleted;
        Files = files.OrderBy(x => x.Name.CanonicalName).ToImmutableArray();
        Directories = directories.OrderBy(x => x.CanonicalName).ToImmutableArray();
    }

    [SetsRequiredMembers]
    public VfsDirectoryMetadata(BufferReader reader)
    {
        var serializationVersion = reader.ReadByte();
        switch (serializationVersion)
        {
            case 0:
                {
                    Path = reader.ReadDirectoryPath();
                    ParentVersion = reader.ReadLong();
                    SnapshotTimestamp = reader.ReadTimestamp();
                    IsDeleted = reader.ReadByte() != 0;

                    // Limit number of files in a directory to 10000 to protect against invalid or malicious inputs
                    // This is not a technical limitation so could be raised if needed.
                    int fileCount = reader.ReadInt();
                    if (fileCount > 10000)
                    {
                        throw new Exception("Too many files in directory - limit is 10000");
                    }

                    var filesBuilder = ImmutableArray.CreateBuilder<VfsFileMetadata>();
                    for (int i = 0; i < fileCount; i++)
                    {
                        filesBuilder.Add(reader.ReadFileMetadata());
                    }
                    Files = filesBuilder.OrderBy(x => x.Name.CanonicalName).ToImmutableArray();

                    int directoryCount = reader.ReadInt();
                    if (directoryCount > 10000)
                    {
                        throw new Exception("Too many subdirectories in directory - limit is 10000");
                    }
                    var directoriesBuilder = ImmutableArray.CreateBuilder<Filename>();
                    for (int i = 0; i < directoryCount; i++)
                    {
                        directoriesBuilder.Add(reader.ReadFilename());
                    }
                    Directories = directoriesBuilder.OrderBy(x => x.CanonicalName).ToImmutableArray();
                }
                break;

            default:
                throw new Exception("Invalid serialization version");
        }
    }

    public int GetSerializedLength() =>
        1 + // SerializationVersion
        Path.GetSerializedLength() +
        ParentVersion.GetSerializedLength() +
        SnapshotTimestamp.GetSerializedLength() +
        1 + // IsDeleted
        Files.Count().GetSerializedLength() +
        Files.Sum(x => x.GetSerializedLength()) +
        Directories.Count().GetSerializedLength() +
        Directories.Sum(x => x.GetSerializedLength());

    public void SerializeTo(BufferBuilder builder)
    {
        builder.AppendByte(0);
        Path.SerializeTo(builder);
        builder.AppendLong(ParentVersion);
        builder.AppendTimestamp(SnapshotTimestamp);
        builder.AppendByte((byte)(IsDeleted ? 1 : 0));
        builder.AppendInt(Files.Count());
        foreach (var file in Files)
        {
            file.SerializeTo(builder);
        }
        builder.AppendInt(Directories.Count());
        foreach (var directory in Directories)
        {
            directory.SerializeTo(builder);
        }
    }

    public virtual bool Equals(VfsDirectoryMetadata? other)
    {
        return other != null &&
            Path == other.Path &&
            ParentVersion == other.ParentVersion &&
            SnapshotTimestamp == other.SnapshotTimestamp &&
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
        return HashCode.Combine(Path, ParentVersion, SnapshotTimestamp, IsDeleted, i);
    }

    public override string ToString()
    {
        return $"Path={Path};ParentVersion={ParentVersion};SnapshotTimestamp{SnapshotTimestamp};IsDeleted{IsDeleted};Files={string.Join(",", Files.OrderBy(x => x.Name.CanonicalName))};Directories={string.Join(",", Directories.OrderBy(x => x.CanonicalName))}";
    }
}
