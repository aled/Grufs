namespace Wibblr.Grufs
{
    public enum CreateDirectoryStatus
    {
        Unknown = 0,
        Success = 1,
        AlreadyExists = 2,
        NonDirectoryAlreadyExists = 3,
        PathNotFound = 4,
        ConnectionError = 5,
        PermissionError = 6,
        UnknownError = 7,
    }

    public enum ReadFileStatus
    {
        Unknown = 0,
        Success = 1,
        PathNotFound = 2,
        ConnectionError = 3,
        UnknownError = 4,
    }

    public enum WriteFileStatus
    {
        Unknown = 0,
        Success = 1,
        OverwriteDenied= 2,
        PathNotFound = 4,
        Error = 5,
    }

    public class StoragePath
    {
        public static readonly char[] DirectorySeparators = { '\\', '/' };

        public bool IsRoot;
        public string[] Parts { get; init; }
        public char DirectorySeparator { get; init; }

        public StoragePath(string path, char directorSeparator) 
            : this(path.Split(DirectorySeparators, StringSplitOptions.RemoveEmptyEntries), directorSeparator) 
        {
            IsRoot = path.Length > 0 && DirectorySeparators.Contains(path[0]);
        }

        public StoragePath(string[] parts, char directorySeparator)
        {
            if (parts.Contains("..") || parts.Contains("."))
            {
                throw new Exception("Directory path cannot contain '.' or '..' directories");
            }

            if (parts.Length > 100)
            {
                throw new Exception("Directory tree too deep");
            }

            Parts = parts;
            DirectorySeparator = directorySeparator;
        }

        private StoragePath(StoragePath prefix, StoragePath suffix)
        {
            DirectorySeparator = prefix.DirectorySeparator;
            IsRoot = prefix.IsRoot;
            Parts = new string[prefix.Depth + suffix.Depth];
            Array.Copy(prefix.Parts, Parts, prefix.Depth);
            Array.Copy(suffix.Parts, 0, Parts, prefix.Depth, suffix.Depth);
        }

        public StoragePath Parent => new StoragePath(Parts.SkipLast(1).ToArray(), DirectorySeparator);

        public int Depth => Parts.Length;

        public StoragePath Concat(StoragePath other)
        {
            return new StoragePath(this, other);
        }

        public StoragePath Concat(string other)
        {
            var otherPath = new StoragePath(other, DirectorySeparator);
            return new StoragePath(this, otherPath);
        }

        public override string ToString()
        {
            return IsRoot
                ? DirectorySeparator + string.Join(DirectorySeparator, Parts)
                : string.Join(DirectorySeparator, Parts);
        }
    }

    public abstract class AbstractFileStorage : IChunkStorage
    {
        public static char _directorySeparator;

        public string BaseDir { get; }

        abstract public (List<string> files, List<string> directories) ListDirectoryEntries(string relativePath);

        abstract public ReadFileStatus ReadFile(string relativePath, out byte[] bytes);

        abstract public WriteFileStatus WriteFile(string relativePath, byte[] content, OverwriteStrategy overwrite);

        abstract public bool Exists(string relativePath);

        abstract public CreateDirectoryStatus CreateDirectory(string relativePath);

        abstract public void DeleteDirectory(string relativePath);

        public IEnumerable<StoragePath> ListFiles(string relativePath, bool recursive = true)
        {
            var originalPath = new StoragePath(relativePath, _directorySeparator);
            var stack = new Stack<StoragePath>();
            stack.Push(originalPath);

            while (stack.Any())
            {
                var currentRelativePath = stack.Pop();

                var prefix = new StoragePath(currentRelativePath.Parts.Skip(originalPath.Depth).ToArray(), _directorySeparator);

                var (files, directories) = ListDirectoryEntries(currentRelativePath.ToString());

                foreach (var file in files)
                {
                    yield return prefix.Concat(file);
                }

                if (recursive)
                {
                    foreach (var directory in directories)
                    {
                        stack.Push(currentRelativePath.Concat(directory));
                    }
                }
            }
        }

        protected AbstractFileStorage(string baseDir, char directorySeparator)
        {
            if (!StoragePath.DirectorySeparators.Contains(directorySeparator))
            {
                throw new ArgumentException($"Invalid directory separator (must be one of {"'" + string.Join("', '", StoragePath.DirectorySeparators) + "'"})");
            }

            _directorySeparator = directorySeparator;
            BaseDir = baseDir;
        }

        public WriteFileStatus WriteFile(string relativePath, byte[] content, OverwriteStrategy overwrite, bool createDirectories)
        {
            var result = WriteFile(relativePath, content, overwrite);

            switch (result)
            {
                case WriteFileStatus.Success:
                case WriteFileStatus.OverwriteDenied:
                case WriteFileStatus.Error:
                    return result;

                case WriteFileStatus.PathNotFound:
                    {
                        if (!createDirectories)
                        {
                            return WriteFileStatus.Error;
                        }

                        var storagePath = new StoragePath(relativePath, _directorySeparator);
                        if (storagePath.Depth < 2)
                        {
                            return WriteFileStatus.Error;
                        }

                        if (!CreateDirectory(storagePath.Parent.ToString(), createParents: true))
                        {
                            return WriteFileStatus.Error;
                        }

                        // This can recurse only once
                        return WriteFile(relativePath, content, overwrite, false);
                    }

                default:
                    throw new Exception();
            }
        }

        public bool CreateDirectory(string relativePath, bool createParents)
        {
            var result = CreateDirectory(relativePath);
            
            switch (result)
            {
                case CreateDirectoryStatus.Success:
                case CreateDirectoryStatus.AlreadyExists:
                    return true;

                case CreateDirectoryStatus.NonDirectoryAlreadyExists:
                    return false;

                case CreateDirectoryStatus.PathNotFound:
                    if (!createParents)
                    {
                        return false;
                    }
                    var storagePath = new StoragePath(relativePath, _directorySeparator);
                    if (storagePath.Depth > 1)
                    {
                        storagePath = storagePath.Parent;
                        if (!CreateDirectory(storagePath.ToString(), true))
                        {
                            return false;
                        }
                    }
                    return CreateDirectory(relativePath, false);

                default:
                    return false;
            }
        }

        private string GeneratePath(string key)
        {
            if (key.Length < 6)
                throw new ArgumentException(key);

            // convert files of the form abcdefghijk to ab/cd/abcdefjhijk
            // to reduce the number of files per directory.
            var path = string.Create(key.Length + 6, key, (chars, key) =>
            {
                chars[0] = key[0];
                chars[1] = key[1];
                chars[2] = _directorySeparator;
                chars[3] = key[2];
                chars[4] = key[3];
                chars[5] = _directorySeparator;

                key.AsSpan().CopyTo(chars.Slice(6));
            });

            return path;
        }

        public IEnumerable<string> GetParentDirectories(StoragePath path)
        {
            for (int i = 0; i < path.Depth; i++)
            {
                yield return string.Join(_directorySeparator, path.Parts.Take(i + 1));
            }
        }

        bool IChunkStorage.Exists(Address address)
        {
            var path = GeneratePath(address.ToString());

            return Exists(path);
        }

        IEnumerable<Address> IChunkStorage.ListAddresses()
        {
            foreach (var path in ListFiles(""))
            {
                if (path.Depth == 3 && 
                    path.Parts[0].Length == 2 && 
                    path.Parts[1].Length == 2 && 
                    path.Parts[2].Length == Address.Length * 2)
                {
                    byte[]? bytes = null;
                    try
                    {
                        bytes = Convert.FromHexString(path.Parts[2]);
                    }
                    catch (Exception) { }

                    if (bytes != null) yield return new Address(bytes);
                }
            }
        }

        PutStatus IChunkStorage.Put(EncryptedChunk chunk, OverwriteStrategy overwrite)
        {
            var path = GeneratePath(chunk.Address.ToString());
            var retry = false;

            do
            {
                var result = WriteFile(path, chunk.Content, overwrite);

                switch (result)
                {
                    case WriteFileStatus.Success:
                        return PutStatus.Success;

                    case WriteFileStatus.OverwriteDenied:
                        return PutStatus.OverwriteDenied;

                    case WriteFileStatus.PathNotFound:
                        if (CreateDirectory(new StoragePath(path, _directorySeparator).Parent.ToString(), createParents: true))
                        {
                            retry = true;
                            continue;
                        }
                        break;

                    default:
                        return PutStatus.Error;
                }
            } while (retry);

            return PutStatus.Error;
        }

        bool IChunkStorage.TryGet(Address address, out EncryptedChunk chunk)
        {
            var relativePath = GeneratePath(address.ToString());

            var result = ReadFile(relativePath, out var bytes);

            switch (result)
            {
                case ReadFileStatus.Success:
                    chunk = new EncryptedChunk(address, bytes);
                    return true;

                default:
                    chunk = default;
                    return false;
            }
        }
    }
}