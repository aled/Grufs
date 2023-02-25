using System.Collections.Immutable;
using System.IO.Compression;
using System.Text;

using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Filesystem
{
    public class VirtualFilesystem
    {
        private readonly string _keyNamespace;
        private readonly VersionedDictionary _dictionaryStorage;
        private readonly StreamStorage _streamStorage;

        public VirtualFilesystem(Repository repository, string filesystemName)
        {
            _keyNamespace = $"virtual-filesystem:{filesystemName.Length}-{filesystemName}";
            var chunkEncryptor = new ChunkEncryptor(repository.MasterKey, repository.VersionedDictionaryAddressKey, new Compressor(CompressionAlgorithm.Brotli, CompressionLevel.Optimal));
            _dictionaryStorage = new VersionedDictionary(_keyNamespace, repository.ChunkStorage, chunkEncryptor);
            _streamStorage = repository.StreamStorage;
        }

        private ReadOnlySpan<byte> GetDirectoryLookupKey(string path) => Encoding.UTF8.GetBytes(path);

        private (VirtualDirectory?, long version) GetLatestVirtualDirectory(DirectoryPath path, long hintVersion = 0)
        {
            var directoryLookupKey = GetDirectoryLookupKey(path.CanonicalPath);

            var nextVersion = _dictionaryStorage.GetNextSequenceNumber(directoryLookupKey, hintVersion);

            if (nextVersion == 0)
            {
                return (null, 0);
            }

            var currentVersion = nextVersion - 1;
            if (_dictionaryStorage.TryGetValue(directoryLookupKey, currentVersion, out var buffer))
            {
                return (new VirtualDirectory(new BufferReader(buffer)), currentVersion);
            }

            throw new Exception("Missing directory version");
        }

        // TODO: return stats
        private (VirtualDirectory, long version) WriteVirtualDirectoryVersion(VirtualDirectory directory, long version)
        {
            var serialized = new BufferBuilder(directory.GetSerializedLength()).AppendVirtualDirectory(directory).ToBuffer();
            var lookupKey = GetDirectoryLookupKey(directory.Path.CanonicalPath);
            //TODO return status as enum
            _dictionaryStorage.TryPutValue(lookupKey, version, serialized.AsSpan());
            return (directory, version);
        }

        private (VirtualDirectory, long version) EnsureDirectoryContains(DirectoryPath directoryPath, Filename childDirectoryName, long parentVersion, Timestamp snapshotTimestamp)
        {
            var (virtualDirectory, version) = GetLatestVirtualDirectory(directoryPath);

            // If directory exists but the latest version does not contain the child directory, then update directory to contain the new child 
            if (virtualDirectory != null)
            {
                if (virtualDirectory.Directories.Contains(childDirectoryName))
                {
                    return (virtualDirectory, version);
                }

                return WriteVirtualDirectoryVersion(
                    virtualDirectory with { Directories = virtualDirectory.Directories.Add(childDirectoryName), SnapshotTimestamp = snapshotTimestamp },
                    version + 1);
            }

            // directory does not exist, create.
            return WriteVirtualDirectoryVersion(
                new VirtualDirectory(directoryPath, parentVersion, snapshotTimestamp, false, new FileMetadata[0], new Filename[] { childDirectoryName }),
                0);
        }

        // TODO: refactor this
        public (VirtualDirectory, long version, StreamWriteStats stats) UploadDirectoryRecursive(string localDirectoryPath, DirectoryPath directoryPath, bool recursive = true)
        {
            var snapshotTimestamp = new Timestamp(DateTime.UtcNow);

            (VirtualDirectory, long version, StreamWriteStats stats) UploadDirectoryRecursive(string localDirectoryPath, DirectoryPath directoryPath, long parentVersion, bool recursive)
            {
                var cumulativeStats = new StreamWriteStats();

                // Upload all local files, recording the address/chunk type/other metadata of each
                var filesBuilder = ImmutableArray.CreateBuilder<FileMetadata>();
                var directoriesBuilder = ImmutableArray.CreateBuilder<Filename>();

                var di = new DirectoryInfo(localDirectoryPath);
                if (!di.Exists)
                {
                    throw new Exception("Invalid directory");
                }

                foreach (var fsi in di.EnumerateFileSystemInfos())
                {
                    if (fsi is FileInfo file)
                    {
                        try
                        {
                            using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                var (address, level, fileStats) = _streamStorage.Write(stream);
                                cumulativeStats.Add(fileStats);
                                Console.WriteLine($"{file.FullName}, {fileStats}");
                                filesBuilder.Add(new FileMetadata(new Filename(file.Name), address, level, snapshotTimestamp, new Timestamp(file.LastWriteTimeUtc), file.Length));
                            }
                        }
                        catch (IOException)
                        {
                            Console.WriteLine($"IO Exception for {file.FullName}");
                        }
                        catch (UnauthorizedAccessException)
                        {
                            Console.WriteLine($"Unauthorized access for {file.FullName}");
                        }
                    }
                    if (fsi is DirectoryInfo dir)
                    {
                        directoriesBuilder.Add(new Filename(dir.Name));
                    }
                }

                // upload this directory
                var (directory, version) = GetLatestVirtualDirectory(directoryPath);
                (VirtualDirectory directory, long version) ret;

                if (directory == null)
                {
                    ret = WriteVirtualDirectoryVersion(new VirtualDirectory(directoryPath, 0, snapshotTimestamp, false, filesBuilder.ToImmutableArray(), directoriesBuilder.ToImmutableArray()), 0);
                    //Console.WriteLine($"Write new virtual directory version: {directoryPath}, {version}");
                }
                else
                {
                    // TODO: surface this option from the UI
                    var deleteOption = true;
                    if (!deleteOption)
                    {
                        var preExistingFiles = directory.Files.Except(filesBuilder);
                        filesBuilder.AddRange(preExistingFiles);

                        var preExistingDirectories = directory.Directories.Except(directoriesBuilder);
                        directoriesBuilder.AddRange(preExistingDirectories);
                    }

                    var updated = recursive
                        ? directory with { Files = filesBuilder.OrderBy(x => x.Name.CanonicalName).ToImmutableArray(), Directories = directoriesBuilder.OrderBy(x => x.CanonicalName).ToImmutableArray() }
                        : directory with { Files = filesBuilder.OrderBy(x => x.Name.CanonicalName).ToImmutableArray() };

                    if (!updated.Equals(directory))
                    {
                        ret = WriteVirtualDirectoryVersion(updated, version + 1);
                    }
                    else
                    {
                        // directory unchanged; return existing version
                        ret = (directory, version);
                    }
                }

                foreach (var d in directoriesBuilder)
                {
                    var (_, _, stats) = UploadDirectoryRecursive(new DirectoryPath(di.FullName) + "/" + d, new DirectoryPath(directoryPath + "/" + d), version + 1, recursive);
                    cumulativeStats.Add(stats);
                }

                return (ret.directory, ret.version, cumulativeStats);
            }

            long parentVersion = 0; // default value for root directory

            // Starting at the root and going down to the parent of this directory, ensure each directory exists and contains the child directory
            // Note we do not go down to this directory, as it also needs the files before we write it.
            foreach (var (dirPath, childDirName) in directoryPath.PathHierarchy())
            {
                // ensure that dir exists, and contains childDir
                (_, parentVersion) = EnsureDirectoryContains(dirPath, childDirName, parentVersion, snapshotTimestamp);
            }

            return UploadDirectoryRecursive(localDirectoryPath, directoryPath, parentVersion, recursive);
        }

        // Non recursive upload of a local directory to the repository
        public (VirtualDirectory, long version) UploadDirectoryNonRecursive(string localDirectoryPath, DirectoryPath directoryPath)
        {
            var snapshotTimestamp = new Timestamp(DateTime.UtcNow);

            long parentVersion = 0; // default value for root directory

            // Starting at the root and going down to the parent of this directory, ensure each directory exists and contains the child directory
            // Note we do not go down to this directory, as it also needs the files before we write it.
            foreach (var (dirPath, childDirName) in directoryPath.PathHierarchy())
            {
                // ensure that dir exists, and contains childDir
                (_, parentVersion) = EnsureDirectoryContains(dirPath, childDirName, parentVersion, snapshotTimestamp);
            }

            // Upload all local files, recording the address/chunk type/other metadata of each
            // Ignore directories.
            var filesBuilder = ImmutableArray.CreateBuilder<FileMetadata>();

            var di = new DirectoryInfo(localDirectoryPath);
            if (!di.Exists)
            {
                throw new Exception("Invalid directory");
            }

            foreach (var file in di.EnumerateFiles())
            {
                using (var stream = new FileStream(file.FullName, FileMode.Open))
                {
                    var (address, level, stats) = _streamStorage.Write(stream);
                    filesBuilder.Add(new FileMetadata(new Filename(file.Name), address, level, snapshotTimestamp, new Timestamp(file.LastWriteTimeUtc), file.Length));
                }
            }

            // finally upload this directory
            var (directory, version) = GetLatestVirtualDirectory(directoryPath);

            if (directory == null)
            {
                return WriteVirtualDirectoryVersion(new VirtualDirectory(directoryPath, parentVersion, snapshotTimestamp, false, filesBuilder.ToImmutableArray(), new Filename[0]), 0);
            }

            var oldFiles = directory.Files.Except(filesBuilder);
            filesBuilder.AddRange(oldFiles);

            var updated = directory with { Files = filesBuilder.ToImmutableArray() };
            return WriteVirtualDirectoryVersion(updated, version + 1);
        }

        public void ListDirectory(DirectoryPath path)
        {
            var stack = new Stack<DirectoryPath>();
            stack.Push(path);

            while (stack.Any())
            {
                var (directory, version) = GetLatestVirtualDirectory(stack.Pop());

                if (directory == null)
                {
                    continue;
                }
                Console.WriteLine(directory.SnapshotTimestamp.ToString("yyyy-MM-dd HH:mm:ss") + " " +  version.ToString("0000") + " " + directory.Path.NormalizedPath);
                foreach (var file in directory.Files)
                {
                    Console.WriteLine(directory.SnapshotTimestamp.ToString("yyyy-MM-dd HH:mm:ss") + "      " + directory.Path.NormalizedPath + "/" + file.Name.ToString());
                }
                foreach (var subDir in directory.Directories)
                {
                    stack.Push(new DirectoryPath(directory.Path + "/" + subDir.OriginalName));
                }
            }
        }

        public void Download(DirectoryPath path, Filename? filename, string localDirectoryPath, bool recursive)
        {
            var (directory, version) = GetLatestVirtualDirectory(path);

            if (directory == null)
            {
                throw new Exception($"Virtual Directory not found: '{path}'");
            }

            Directory.CreateDirectory(localDirectoryPath);

            foreach (var file in directory.Files)
            {
                if (filename == null || file.Name == filename)
                {
                    var level = file.IndexLevel;
                    var address = file.Address;

                    var buffers = _streamStorage.Read(level, address);
                    var localPath = Path.Join(localDirectoryPath, file.Name.OriginalName);
                    using (var stream = new FileStream(localPath, FileMode.Create))
                    {
                        var bytesWritten = 0;

                        foreach (var buffer in buffers)
                        {
                            stream.Write(buffer.AsSpan());
                            bytesWritten += buffer.AsSpan().Length;
                            Console.CursorVisible = false;
                            Console.Write(localPath + " " + bytesWritten + "/" + file.Size);
                            Console.CursorLeft = 0;
                        }
                        Console.WriteLine();
                    }
                }
            }

            if (recursive)
            {
                foreach (var subdir in directory.Directories)
                {
                    Download(new DirectoryPath(path + "/" + subdir), null, Path.Join(localDirectoryPath, subdir.OriginalName), true);
                }
            }
        }

        public void Scrub(DirectoryPath path, Filename filename)
        {

        }
    }
}
