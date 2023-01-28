using System.Collections.Immutable;
using System.IO.Compression;
using System.Text;

using Wibblr.Grufs.Core;

namespace Wibblr.Grufs
{
    public class MutableFilesystem
    {
        private readonly string _keyNamespace;
        private readonly VersionedDictionaryStorage _dictionaryStorage;
        private readonly StreamStorage _streamStorage;

        public MutableFilesystem(Repository repository, string filesystemName)
        {
            _keyNamespace = $"mutable-filesystem:{filesystemName.Length}-{filesystemName}";
            var chunkEncryptor = new ChunkEncryptor(repository.MasterKey, repository.VersionedDictionaryAddressKey, new Compressor(CompressionAlgorithm.Brotli, CompressionLevel.Optimal));
            _dictionaryStorage = new VersionedDictionaryStorage(_keyNamespace, repository.ChunkStorage, chunkEncryptor);
            _streamStorage = repository.StreamStorage;
        }

        private ReadOnlySpan<byte> GetDirectoryLookupKey(string path) => Encoding.UTF8.GetBytes(path);

        private (MutableDirectory?, long version) GetLatestMutableDirectory(DirectoryPath path, long hintVersion = 0)
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
                return (new MutableDirectory(new BufferReader(buffer)), currentVersion);
            }

            throw new Exception("Missing directory version");
        }

        private (MutableDirectory, long version) WriteMutableDirectoryVersion(MutableDirectory directory, long version)
        {
            var serialized = new BufferBuilder(directory.GetSerializedLength()).AppendMutableDirectory(directory).ToBuffer();
            var lookupKey = GetDirectoryLookupKey(directory.Path.CanonicalPath);
            _dictionaryStorage.TryPutValue(lookupKey, version, serialized.AsSpan());
            return (directory, version);
        }

        private (MutableDirectory, long version) EnsureDirectoryContains(DirectoryPath directoryPath, Filename childDirectoryName, long parentVersion)
        {
            var (mutableDirectory, version) = GetLatestMutableDirectory(directoryPath);

            // If directory exists but the latest version does not contain the child directory, then update directory to contain the new child 
            if (mutableDirectory != null)
            {
                if (mutableDirectory.Directories.Contains(childDirectoryName))
                {
                    return (mutableDirectory, version);
                }

                return WriteMutableDirectoryVersion(
                    mutableDirectory with { Directories = mutableDirectory.Directories.Add(childDirectoryName) },
                    version + 1);
            }

            // directory does not exist, create.
            return WriteMutableDirectoryVersion(
                new MutableDirectory(directoryPath, parentVersion, Timestamp.Now, false, new FileMetadata[0], new Filename[] { childDirectoryName }),
                0);
        }

        // TODO: refactor this
        public (MutableDirectory, long version) UploadDirectoryRecursive(string localDirectoryPath, DirectoryPath directoryPath, bool recursive = true)
        {
            (MutableDirectory, long version) UploadDirectoryRecursive(string localDirectoryPath, DirectoryPath directoryPath, long parentVersion, bool recursive)
            {
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
                        using (var stream = new FileStream(file.FullName, FileMode.Open))
                        {
                            var (address, level) = _streamStorage.Write(stream);
                            Console.WriteLine($"Wrote file {file.FullName} to {directoryPath.NormalizedPath}/{file.Name}");
                            filesBuilder.Add(new FileMetadata(new Filename(file.Name), address, level, new Timestamp(file.LastWriteTimeUtc)));
                        }
                    }
                    if (fsi is DirectoryInfo dir)
                    {
                        directoriesBuilder.Add(new Filename(dir.Name));
                    }
                }

                // upload this directory
                var (directory, version) = GetLatestMutableDirectory(directoryPath);
                (MutableDirectory, long version) ret;

                if (directory == null)
                {
                    ret = WriteMutableDirectoryVersion(new MutableDirectory(directoryPath, 0, new Timestamp(di.LastWriteTimeUtc), false, filesBuilder.ToImmutableArray(), new Filename[0]), 0);
                }
                else
                {
                    var preExistingFiles = directory.Files.Except(filesBuilder);
                    filesBuilder.AddRange(preExistingFiles);

                    var preExistingDirectories = directory.Directories.Except(directoriesBuilder);
                    directoriesBuilder.AddRange(preExistingDirectories);

                    var updated = recursive
                        ? directory with { Files = filesBuilder.ToImmutableArray(), Directories = directoriesBuilder.ToImmutableArray() }
                        : directory with { Files = filesBuilder.ToImmutableArray() };
     
                    ret = WriteMutableDirectoryVersion(updated, version + 1);
                }

                foreach (var d in directoriesBuilder)
                {
                    UploadDirectoryRecursive(new DirectoryPath(di.FullName) + "/" + d, new DirectoryPath(directoryPath + "/" + d), version + 1, recursive);
                }

                return ret;
            }

            long parentVersion = 0; // default value for root directory

            // Starting at the root and going down to the parent of this directory, ensure each directory exists and contains the child directory
            // Note we do not go down to this directory, as it also needs the files before we write it.
            foreach (var (dirPath, childDirName) in directoryPath.PathHierarchy())
            {
                // ensure that dir exists, and contains childDir
                (_, parentVersion) = EnsureDirectoryContains(dirPath, childDirName, parentVersion);
            }

            return UploadDirectoryRecursive(localDirectoryPath, directoryPath, parentVersion, recursive);
        }

        // Non recursive upload of a local directory to the repository
        public (MutableDirectory, long version) UploadDirectoryNonRecursive(string localDirectoryPath, DirectoryPath directoryPath)
        {
            long parentVersion = 0; // default value for root directory

            // Starting at the root and going down to the parent of this directory, ensure each directory exists and contains the child directory
            // Note we do not go down to this directory, as it also needs the files before we write it.
            foreach (var (dirPath, childDirName) in directoryPath.PathHierarchy())
            {
                // ensure that dir exists, and contains childDir
                (_, parentVersion) = EnsureDirectoryContains(dirPath, childDirName, parentVersion);
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
                    var (address, level) = _streamStorage.Write(stream);
                    filesBuilder.Add(new FileMetadata(new Filename(file.Name), address, level, new Timestamp(file.LastWriteTimeUtc)));
                }
            }

            // finally upload this directory
            var (directory, version) = GetLatestMutableDirectory(directoryPath);

            if (directory == null)
            {
                return WriteMutableDirectoryVersion(new MutableDirectory(directoryPath, parentVersion, new Timestamp(di.LastWriteTimeUtc), false, filesBuilder.ToImmutableArray(), new Filename[0]), 0);
            }

            var oldFiles = directory.Files.Except(filesBuilder);
            filesBuilder.AddRange(oldFiles);

            var updated = directory with { Files = filesBuilder.ToImmutableArray() };
            return WriteMutableDirectoryVersion(updated, version + 1);
        }

        public void ListDirectory(DirectoryPath path)
        {
            var stack = new Stack<DirectoryPath>();
            stack.Push(path);

            while (stack.Any())
            {
                var (directory, version) = GetLatestMutableDirectory(stack.Pop());

                if (directory == null)
                {
                    continue;
                }
                Console.WriteLine(directory.Path.NormalizedPath + "(v" + version + ") " + directory.LastModifiedTimestamp);
                foreach (var file in directory.Files)
                {
                    Console.WriteLine(directory.Path.NormalizedPath + "/" + file.Name.ToString() + " " + file.LastModifiedTimestamp + "(" + file.Address.ToString().Substring(0, 6) + ")");
                }
                foreach (var subDir in directory.Directories)
                {
                    stack.Push(new DirectoryPath(directory.Path + "/" + subDir.OriginalName));
                }
            }
        }

        public void DownloadFile(DirectoryPath path, Filename filename, string localDirectoryPath)
        {
            var stream = new FileStream(Path.Join(localDirectoryPath, filename.OriginalName), FileMode.CreateNew);

            var (directory, version) = GetLatestMutableDirectory(path);

            var file = directory.Files.SingleOrDefault(x => x.Name == filename);
            var level = file.IndexLevel;
            var address = file.Address;

            var buffers = _streamStorage.Read(level, address);

            foreach (var buffer in buffers)
            {
                stream.Write(buffer.AsSpan());
            }
        }

        public void Scrub(DirectoryPath path, Filename filename)
        {

        }
    }
}
