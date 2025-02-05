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

        private byte[] GetDirectoryLookupKey(string path) => Encoding.UTF8.GetBytes(path);

        private async Task<VfsDirectoryMetadata?> GetVirtualDirectoryAsync(DirectoryPath path, long version, CancellationToken token)
        {
            if (version < 0)
            {
                return null;
            }

            var directoryLookupKey = GetDirectoryLookupKey(path.CanonicalPath);

            if ((await _dictionaryStorage.GetValueAsync(directoryLookupKey, version, token)) is ArrayBuffer buffer)
            {
                return new VfsDirectoryMetadata(new BufferReader(buffer));
            }
            return null;
        }

        private async Task<(VfsDirectoryMetadata?, long version)> GetLatestVirtualDirectoryAsync(DirectoryPath path, CancellationToken token, long hintVersion = 0)
        {
            var directoryLookupKey = GetDirectoryLookupKey(path.CanonicalPath);

            var nextVersion = await _dictionaryStorage.GetNextSequenceNumberAsync(directoryLookupKey, hintVersion, token);

            if (nextVersion == 0)
            {
                return (null, 0);
            }

            var currentVersion = nextVersion - 1;
            var buffer = await _dictionaryStorage.GetValueAsync(directoryLookupKey, currentVersion, token);

            if (buffer != ArrayBuffer.Empty)
            {
                return (new VfsDirectoryMetadata(new BufferReader(buffer)), currentVersion);
            }

            throw new Exception("Missing directory version");
        }

        // TODO: return stats
        private async Task<(VfsDirectoryMetadata, long version)> WriteVirtualDirectoryVersionAsync(VfsDirectoryMetadata directory, long version, CancellationToken token)
        {
            var serialized = new BufferBuilder(directory.GetSerializedLength()).AppendVirtualDirectory(directory).ToBuffer();
            var lookupKey = GetDirectoryLookupKey(directory.Path.CanonicalPath);
            //TODO return status as enum
            await _dictionaryStorage.PutValueAsync(lookupKey, version, serialized, token);
            return (directory, version);
        }

        private async Task<(VfsDirectoryMetadata, long version)> EnsureDirectoryContainsAsync(DirectoryPath directoryPath, Filename childDirectoryName, long parentVersion, Timestamp vfsLastModified, CancellationToken token)
        {
            var (virtualDirectory, version) = await GetLatestVirtualDirectoryAsync(directoryPath, token);

            // If directory exists but the latest version does not contain the child directory, then update directory to contain the new child 
            if (virtualDirectory != null)
            {
                if (virtualDirectory.Directories.Contains(childDirectoryName))
                {
                    return (virtualDirectory, version);
                }

                return await WriteVirtualDirectoryVersionAsync(
                    virtualDirectory with { Directories = virtualDirectory.Directories.Add(childDirectoryName), VfsLastModified = vfsLastModified },
                    version + 1,
                    token);
            }

            // directory does not exist, create.
            return await WriteVirtualDirectoryVersionAsync(
                new VfsDirectoryMetadata(directoryPath, parentVersion, vfsLastModified, false, new VfsFileMetadata[0], new Filename[] { childDirectoryName }),
                0,
                token);
        }

        public async Task<(VfsDirectoryMetadata, long version, StreamWriteStats stats)> UploadDirectoryAsync(string localRootDirectoryPath, string vfsDirectoryPath, bool recursive = true, CancellationToken token = default)
        {
            var vfsLastModified = new Timestamp(DateTime.UtcNow);
            var directoryPath = new DirectoryPath(vfsDirectoryPath.Substring(vfsPrefix.Length));

            async Task<(VfsDirectoryMetadata, long version, StreamWriteStats stats)> UploadDirectoryAsync(string localDirectoryPath, DirectoryPath directoryPath, long parentVersion, bool recursive)
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

                            var (address, level, fileStats) = await _streamStorage.WriteAsync(stream, token);
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
                var (vfsDirectory, version) = await GetLatestVirtualDirectoryAsync(directoryPath, token);
                (VfsDirectoryMetadata directory, long version) ret;

                if (vfsDirectory == null)
                {
                    ret = await WriteVirtualDirectoryVersionAsync(new VfsDirectoryMetadata(directoryPath, 0, vfsLastModified, false, filesBuilder.ToImmutableArray(), directoriesBuilder.ToImmutableArray()), 0, token);

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
                        ret = await WriteVirtualDirectoryVersionAsync(updated, version + 1, token);
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
                        var (_, _, stats) = await UploadDirectoryAsync(Path.Join(di.FullName, d), new DirectoryPath(directoryPath + "/" + d), version + 1, recursive);
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
                (_, parentVersion) = await EnsureDirectoryContainsAsync(dirPath, childDirName, parentVersion, vfsLastModified, token);
            }

            var ret = await UploadDirectoryAsync(localRootDirectoryPath, directoryPath, parentVersion, recursive);
            _chunkStorage.Flush();
            return ret;
        }

        public async Task<(VfsDirectoryMetadata?, long)> GetDirectoryAsync(DirectoryPath path, Timestamp timestamp, CancellationToken token)
        {
            // TODO: lookup using a binary search instead of linear.

            var (metadata, version) = await GetLatestVirtualDirectoryAsync(path, token);

            // while the directory snapshot timestamp is later than the given timestamp, get the next earlier version
            while (version > 0 && metadata != null && metadata.VfsLastModified > timestamp)
            {
                version -= 1;
                metadata = await GetVirtualDirectoryAsync(path, version, token);
            }

            return (metadata, version);
        }

        public async Task<(VfsDirectoryMetadata?, long)> GetDirectoryAsync(string path, Timestamp timestamp, CancellationToken token)
        {
            var (isVfs, exists, isDirectory) = await AnalyzePathAsync(path, token);

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
            return await GetDirectoryAsync(directoryPath, timestamp, token);
        }

        public async Task ListDirectoryRecursiveAsync(DirectoryPath path, CancellationToken token)
        {
            var stack = new Stack<DirectoryPath>();
            stack.Push(path);

            while (stack.Any())
            {
                var (directory, version) = await GetLatestVirtualDirectoryAsync(stack.Pop(), token);

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

        private async Task DownloadDirectoryAsync(string vfsDirectoryPath, string localDirectoryPath, bool recursive, CancellationToken token)
        {
            var directoryPath = new DirectoryPath(vfsDirectoryPath.Substring(vfsPrefix.Length));

            var (directory, version) = await GetLatestVirtualDirectoryAsync(directoryPath, token);

            if (directory == null)
            {
                throw new Exception($"Virtual Directory not found: '{directoryPath}'");
            }

            Directory.CreateDirectory(localDirectoryPath);

            foreach (var file in directory.Files)
            {
                var level = file.IndexLevel;
                var address = file.Address;

                var buffers = _streamStorage.ReadAsync(level, address, token);
                var localPath = Path.Join(localDirectoryPath, file.Name.OriginalName);
                using (var stream = new FileStream(localPath, FileMode.Create))
                {
                    var bytesWritten = 0;

                    await foreach (var buffer in buffers)
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
                    await DownloadDirectoryAsync(
                        vfsPrefix + directoryPath.ToString() + "/" + subdir, 
                        Path.Join(localDirectoryPath, subdir.OriginalName), 
                        recursive: true,
                        token);
                }
            }
        }

        public void Scrub(DirectoryPath path, Filename filename)
        {

        }

        internal async Task<(bool isVfs, bool exists, bool isDirectory)> AnalyzePathAsync(string path, CancellationToken token)
        {
            var isVfs = path.StartsWith(vfsPrefix);

            if (isVfs)
            {
                path = path.Substring(vfsPrefix.Length);
            }

            Func<string, Task<bool>> VfsDirectoryExists = async path =>
            {
                try
                {
                    var (virtualDirectory, version) = await GetLatestVirtualDirectoryAsync(new DirectoryPath(path), token);
                    return virtualDirectory != null;
                }
                catch (Exception)
                {
                    return false;
                }
            };

            Func<string, Task<bool>> VfsFileExists = async path =>
            {
                var (parent, file) = path.SplitLast('/');
                try
                {
                    var (virtualDirectory, version) = await GetLatestVirtualDirectoryAsync(new DirectoryPath(parent), token);
                    return virtualDirectory?.Files.Any(x => x.Name == new Filename(file)) ?? false;
                }
                catch (Exception)
                {
                    return false;
                }
            };

            // Check if it is a directory
            if ((isVfs && await VfsDirectoryExists(path)) || Directory.Exists(path))
            {
                return (isVfs, true, true);
            }

            // if the path ends in a separator, it is a directory, but does not exist
            if (path.EndsWith('/') || path.EndsWith('\\'))
            {
                return (isVfs, false, true);
            }

            if ((isVfs && await VfsFileExists(path)) || File.Exists(path))
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
        public async Task<int> SyncAsync(string sourcePath, string destinationPath, bool recursive, CancellationToken token)
        {
            if (destinationPath.StartsWith(vfsPrefix))
            {
                var (_, _, stats) = await UploadDirectoryAsync(sourcePath, destinationPath, recursive);
                Log.WriteLine(0, stats.ToString(Log.HumanFormatting));
            }
            else if (sourcePath.StartsWith(vfsPrefix))
            {
                await DownloadDirectoryAsync(sourcePath, destinationPath, recursive, token);
            }

            return 0;
        }
    }
}
