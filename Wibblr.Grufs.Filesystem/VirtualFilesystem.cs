using System;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;

using Wibblr.Grufs.Core;
using Wibblr.Grufs.Logging;
using Wibblr.Grufs.Storage;

[assembly: InternalsVisibleTo("Wibblr.Grufs.Tests")]
namespace Wibblr.Grufs.Filesystem
{
    public class VirtualFilesystem
    {
        private readonly string _keyNamespace;
        private readonly VersionedDictionary _dictionaryStorage;
        private readonly IChunkStorage _chunkStorage;
        private readonly StreamStorage _streamStorage;

        private static readonly string vfsPrefix = "vfs://";

        public VirtualFilesystem(Repository repository, string filesystemName)
        {
            _keyNamespace = $"virtual-filesystem:{filesystemName.Length}-{filesystemName}";
            var chunkEncryptor = new ChunkEncryptor(repository.MasterKey, repository.VersionedDictionaryAddressKey, new Compressor(CompressionAlgorithm.Brotli, CompressionLevel.Optimal));
            _dictionaryStorage = new VersionedDictionary(_keyNamespace, repository.ChunkStorage, chunkEncryptor);
            _chunkStorage = repository.ChunkStorage;
            _streamStorage = repository.StreamStorage;
        }

        private ReadOnlySpan<byte> GetDirectoryLookupKey(string path) => Encoding.UTF8.GetBytes(path);

        private VfsDirectoryMetadata? GetVirtualDirectory(DirectoryPath path, long version)
        {
            if (version < 0)
            {
                return null;
            }

            var directoryLookupKey = GetDirectoryLookupKey(path.CanonicalPath);

            if (_dictionaryStorage.TryGetValue(directoryLookupKey, version, out var buffer))
            {
                return new VfsDirectoryMetadata(new BufferReader(buffer));
            }
            return null;
        }

        private (VfsDirectoryMetadata?, long version) GetLatestVirtualDirectory(DirectoryPath path, long hintVersion = 0)
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
                return (new VfsDirectoryMetadata(new BufferReader(buffer)), currentVersion);
            }

            throw new Exception("Missing directory version");
        }

        // TODO: return stats
        private (VfsDirectoryMetadata, long version) WriteVirtualDirectoryVersion(VfsDirectoryMetadata directory, long version)
        {
            var serialized = new BufferBuilder(directory.GetSerializedLength()).AppendVirtualDirectory(directory).ToBuffer();
            var lookupKey = GetDirectoryLookupKey(directory.Path.CanonicalPath);
            //TODO return status as enum
            _dictionaryStorage.TryPutValue(lookupKey, version, serialized.AsSpan());
            return (directory, version);
        }

        private (VfsDirectoryMetadata, long version) EnsureDirectoryContains(DirectoryPath directoryPath, Filename childDirectoryName, long parentVersion, Timestamp vfsLastModified)
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
                    virtualDirectory with { Directories = virtualDirectory.Directories.Add(childDirectoryName), VfsLastModified = vfsLastModified },
                    version + 1);
            }

            // directory does not exist, create.
            return WriteVirtualDirectoryVersion(
                new VfsDirectoryMetadata(directoryPath, parentVersion, vfsLastModified, false, new VfsFileMetadata[0], new Filename[] { childDirectoryName }),
                0);
        }

        public (VfsDirectoryMetadata, long version, StreamWriteStats stats) UploadDirectory(string localRootDirectoryPath, string vfsDirectoryPath, bool recursive = true)
        {
            var vfsLastModified = new Timestamp(DateTime.UtcNow);
            var directoryPath = new DirectoryPath(vfsDirectoryPath.Substring(vfsPrefix.Length));

            (VfsDirectoryMetadata, long version, StreamWriteStats stats) UploadDirectory(string localDirectoryPath, DirectoryPath directoryPath, long parentVersion, bool recursive)
            {
                //Log.WriteLine(0, $"In UploadDirectoryRecursive: {localDirectoryPath}");
                var cumulativeStats = new StreamWriteStats();

                // Upload all local files, recording the address/chunk type/other metadata of each
                var filesBuilder = ImmutableArray.CreateBuilder<VfsFileMetadata>();
                var directoriesBuilder = ImmutableArray.CreateBuilder<Filename>();

                var di = new DirectoryInfo(localDirectoryPath);
                if (!di.Exists)
                {
                    throw new Exception($"Invalid directory {di.FullName}");
                }

                foreach (var file in di.EnumerateFiles())
                {
                    try
                    {
                        using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            // verbose = 0
                            Log.WriteLine(0, Path.GetRelativePath(localRootDirectoryPath, file.FullName));

                            //Log.WriteLine(1, $" {fileStats.ToString(Log.HumanFormatting)}");

                            var (address, level, fileStats) = _streamStorage.Write(stream);
                            cumulativeStats.Add(fileStats);

                            filesBuilder.Add(new VfsFileMetadata(new Filename(file.Name), address, level, vfsLastModified, new Timestamp(file.LastWriteTimeUtc), file.Length));
                        }
                    }
                    catch (IOException)
                    {
                        Log.WriteLine(0, $"IO Exception for {file.FullName}");
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Log.WriteLine(0, $"Unauthorized access for {file.FullName}");
                    }
                }

                foreach (var dir in di.EnumerateDirectories())
                {
                    directoriesBuilder.Add(new Filename(dir.Name));
                }

                // upload this directory
                var (vfsDirectory, version) = GetLatestVirtualDirectory(directoryPath);
                (VfsDirectoryMetadata directory, long version) ret;

                if (vfsDirectory == null)
                {
                    ret = WriteVirtualDirectoryVersion(new VfsDirectoryMetadata(directoryPath, 0, vfsLastModified, false, filesBuilder.ToImmutableArray(), directoriesBuilder.ToImmutableArray()), 0);

                    //Log.WriteLine(0, $"Write new virtual directory version: {directoryPath}, {version}");
                }
                else
                {
                    // TODO: surface this option from the UI
                    var deleteOption = true;
                    if (!deleteOption)
                    {
                        var preExistingFiles = vfsDirectory.Files.Except(filesBuilder);
                        filesBuilder.AddRange(preExistingFiles);

                        var preExistingDirectories = vfsDirectory.Directories.Except(directoriesBuilder);
                        directoriesBuilder.AddRange(preExistingDirectories);
                    }

                    var existingFilesDict = vfsDirectory.Files.ToDictionary(x => x.Name.CanonicalName, x => x);

                    // update the filesbuilder with the vfsLastUpdated timestamp for files that are otherwise identical
                    for (int i = filesBuilder.Count - 1; i >= 0; i--)
                    {
                        // TODO: delete files/directories that are missing on the local, if option is set.

                        // For existing files, use the existing metadata (in order to not modify vfsLastModified)
                        if (existingFilesDict.TryGetValue(filesBuilder[i].Name.CanonicalName, out var existingFileMetadata))
                        {
                            if (existingFileMetadata.Address == filesBuilder[i].Address &&
                                existingFileMetadata.IndexLevel == filesBuilder[i].IndexLevel &&
                                existingFileMetadata.LastModifiedTimestamp == filesBuilder[i].LastModifiedTimestamp &&
                                existingFileMetadata.Size == filesBuilder[i].Size)
                            {
                                filesBuilder[i] = existingFileMetadata;
                            }
                        }
                    }

                    var updated = recursive
                        ? vfsDirectory with { Files = filesBuilder.OrderBy(x => x.Name.CanonicalName).ToImmutableArray(), Directories = directoriesBuilder.OrderBy(x => x.CanonicalName).ToImmutableArray() }
                        : vfsDirectory with { Files = filesBuilder.OrderBy(x => x.Name.CanonicalName).ToImmutableArray() };

                    if (!updated.EqualsIgnoringFilesVfsLastModified(vfsDirectory))
                    {
                        ret = WriteVirtualDirectoryVersion(updated, version + 1);
                    }
                    else
                    {
                        // directory unchanged; return existing version
                        ret = (vfsDirectory, version);
                    }
                }

                if (recursive)
                {
                    foreach (var d in directoriesBuilder)
                    {
                        var (_, _, stats) = UploadDirectory(Path.Join(di.FullName, d), new DirectoryPath(directoryPath + "/" + d), version + 1, recursive);
                        cumulativeStats.Add(stats);
                    }
                }

                // verbose = 0
                Log.Write(0, Path.GetRelativePath(localRootDirectoryPath, localDirectoryPath));
                Log.Write(1, " OK: " + cumulativeStats.ToString(Log.HumanFormatting));
                Log.WriteLine(0, "");

                return (ret.directory, ret.version, cumulativeStats);
            }

            long parentVersion = 0; // default value for root directory

            // Starting at the root and going down to the parent of this directory, ensure each directory exists and contains the child directory
            // Note we do not go down to this directory, as it also needs the files before we write it.
            foreach (var (dirPath, childDirName) in directoryPath.PathHierarchy())
            {
                // ensure that dir exists, and contains childDir
                (_, parentVersion) = EnsureDirectoryContains(dirPath, childDirName, parentVersion, vfsLastModified);
            }

            var ret = UploadDirectory(localRootDirectoryPath, directoryPath, parentVersion, recursive);
            _chunkStorage.Flush();
            return ret;
        }

        public (VfsDirectoryMetadata?, long) GetDirectory(DirectoryPath path, Timestamp timestamp)
        {
            // TODO: lookup using a binary search instead of linear.

            var (metadata, version) = GetLatestVirtualDirectory(path);

            // while the directory snapshot timestamp is later than the given timestamp, get the next earlier version
            while (version > 0 && metadata != null && metadata.VfsLastModified > timestamp)
            {
                version -= 1;
                metadata = GetVirtualDirectory(path, version);
            }

            return (metadata, version);
        }

        public (VfsDirectoryMetadata?, long) GetDirectory(string path, Timestamp timestamp)
        {
            var (isVfs, exists, isDirectory) = AnalyzePath(path);

            if (!isVfs)
            {
                Log.WriteLine(0, $"Path is not a VFS path: {path}");
                return (null, 0);
            }
            
            if (!exists)
            {
                Log.WriteLine(0, $"Path not found: {path}"); 
                return (null, 0);
            }
            
            if (!isDirectory)
            {
                Log.WriteLine(0, $"Path is not a directory: {path}");
                return (null, 0);
            }

            var directoryPath = new DirectoryPath(path.Substring(vfsPrefix.Length));
            return GetDirectory(directoryPath, timestamp);
        }

        public void ListDirectoryRecursive(DirectoryPath path)
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
                Log.WriteLine(0, directory.VfsLastModified.ToString("yyyy-MM-dd HH:mm:ss") + " " + version.ToString("0000") + " " + directory.Path.NormalizedPath);
                foreach (var file in directory.Files)
                {
                    Log.WriteLine(0, directory.VfsLastModified.ToString("yyyy-MM-dd HH:mm:ss") + "      " + directory.Path.NormalizedPath + "/" + file.Name.ToString());
                }
                foreach (var subDir in directory.Directories)
                {
                    stack.Push(new DirectoryPath(directory.Path + "/" + subDir.OriginalName));
                }
            }
        }

        private void DownloadDirectory(string vfsDirectoryPath, string localDirectoryPath, bool recursive)
        {
            var directoryPath = new DirectoryPath(vfsDirectoryPath.Substring(vfsPrefix.Length));

            var (directory, version) = GetLatestVirtualDirectory(directoryPath);

            if (directory == null)
            {
                throw new Exception($"Virtual Directory not found: '{directoryPath}'");
            }

            Directory.CreateDirectory(localDirectoryPath);

            foreach (var file in directory.Files)
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
                        Log.WriteStatusLine(0, localPath + " " + bytesWritten + "/" + file.Size);
                    }
                }
            }

            if (recursive)
            {
                foreach (var subdir in directory.Directories)
                {
                    DownloadDirectory(vfsPrefix + directoryPath.ToString() + "/" + subdir, Path.Join(localDirectoryPath, subdir.OriginalName), true);
                }
            }
        }

        public void Scrub(DirectoryPath path, Filename filename)
        {

        }

        internal (bool isVfs, bool exists, bool isDirectory) AnalyzePath(string path)
        {
            var isVfs = path.StartsWith(vfsPrefix);

            if (isVfs)
            {
                path = path.Substring(vfsPrefix.Length);
            }

            Func<string, bool> VfsDirectoryExists = path =>
            {
                try
                {
                    var (virtualDirectory, version) = GetLatestVirtualDirectory(new DirectoryPath(path));
                    return virtualDirectory != null;
                }
                catch (Exception)
                {
                    return false;
                }
            };

            Func<string, bool> VfsFileExists = path =>
            {
                var (parent, file) = path.SplitLast('/');
                try
                {
                    var (virtualDirectory, version) = GetLatestVirtualDirectory(new DirectoryPath(parent));
                    return virtualDirectory?.Files.Any(x => x.Name == new Filename(file)) ?? false;
                }
                catch (Exception)
                {
                    return false;
                }
            };

            // Check if it is a directory
            var DirectoryExists = isVfs ? VfsDirectoryExists : Directory.Exists;
            if (DirectoryExists(path))
            {
                return (isVfs, true, true);
            }

            // if the path ends in a separator, it is a directory, but does not exist
            if (path.EndsWith('/') || path.EndsWith('\\'))
            {
                return (isVfs, false, true);
            }

            var FileExists = isVfs ? VfsFileExists : File.Exists;
            if (FileExists(path))
            {
                return (isVfs, true, false);
            }

            return (isVfs, false, false);
        }

        /// <summary>
        /// SourcePath and DestinationPath are both directories. 
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destinationPath"></param>
        /// <param name="recursive"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public int Sync(string sourcePath, string destinationPath, bool recursive = true)
        {
            if (destinationPath.StartsWith(vfsPrefix))
            {
                var (_, _, stats) = UploadDirectory(sourcePath, destinationPath, recursive);
                Log.WriteLine(0, stats.ToString(Log.HumanFormatting));
            }
            else if (sourcePath.StartsWith(vfsPrefix))
            {
                DownloadDirectory(sourcePath, destinationPath, recursive);
            }

            return 0;
        }
    }
}
