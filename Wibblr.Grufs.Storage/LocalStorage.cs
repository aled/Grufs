using System;

using Wibblr.Grufs.Logging;

using static Wibblr.Grufs.Storage.FileStorageUtils;

namespace Wibblr.Grufs.Storage
{
    public class LocalStorage : IChunkStorage
    {
        public string BaseDir { get; }

        public LocalStorage(string baseDir)
        {
            BaseDir = Path.TrimEndingDirectorySeparator(baseDir);
        }

        public void Init()
        {
            try
            {
                Directory.CreateDirectory(BaseDir);
                return;
            }
            catch (Exception)
            {
                if (Directory.Exists(BaseDir))
                {
                    return;
                }
                throw new Exception("Error creating basedir");
            }
        }

        public long Count()
        {
            return ListChunkFiles().Count();
        }

        public bool Exists(Address address)
        {
            return File.Exists(Path.Join(BaseDir, GeneratePath(address)));
        }

        public void Flush()
        {
            // no op
        }

        public IEnumerable<Address> ListAddresses()
        {
            foreach (var file in ListChunkFiles())
            {
                yield return new Address(Convert.FromHexString(file.Name));
            }
        }

        private void CreateParentsAndWriteAllBytes(string path, byte[] content)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);

            try
            {
                File.WriteAllBytes(path, content);
            }
            catch (DirectoryNotFoundException)
            {
                var parent = Path.GetDirectoryName(path);
                if (parent != null)
                {
                    Directory.CreateDirectory(parent);
                    File.WriteAllBytes(path, content);
                }
            }
        }

        public PutStatus Put(EncryptedChunk chunk, OverwriteStrategy overwriteStrategy)
        {
            var path = Path.Join(BaseDir, GeneratePath(chunk.Address));

            switch (overwriteStrategy)
            {
                case OverwriteStrategy.Allow:
                    CreateParentsAndWriteAllBytes(path, chunk.Content);
                    return PutStatus.Success;

                case OverwriteStrategy.Deny:
                    if (File.Exists(path))
                    {
                        return PutStatus.OverwriteDenied;
                    }

                    CreateParentsAndWriteAllBytes(path, chunk.Content);
                    return PutStatus.Success;

                default:
                    throw new Exception("Invalid overwrite strategy");
            }
        }

        public bool TryGet(Address address, out EncryptedChunk chunk)
        {
            var path = Path.Join(BaseDir, GeneratePath(address));

            try
            {
                chunk = new EncryptedChunk(address, File.ReadAllBytes(path));
            }
            catch (Exception e)
            {
                //TODO: error handling
                Log.WriteLine(0, e.Message);
                chunk = default;
                return false;
            }

            return true;
        }

        private IEnumerable<FileInfo> ListChunkFiles()
        {
            foreach (var grandparent in new DirectoryInfo(BaseDir).EnumerateDirectories())
            {
                if (grandparent.Name.Length == 2 && IsHexString(grandparent.Name))
                {
                    foreach (var parent in grandparent.EnumerateDirectories())
                    {
                        if (parent.Name.Length == 2 && IsHexString(grandparent.Name))
                        {
                            foreach (var address in parent.EnumerateFiles())
                            {
                                if (address.Name.Length == Address.Length * 2 && IsHexString(address.Name))
                                {
                                    yield return address;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
