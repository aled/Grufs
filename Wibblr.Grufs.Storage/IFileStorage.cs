namespace Wibblr.Grufs
{
    public interface IFileStorage : IChunkStorage
    {
        IFileStorage WithBaseDir(string baseDir);

        IEnumerable<string> GetParentDirectories(string path)
        {
            int lastSeparator = -1;
            while ((lastSeparator = path.IndexOf('/', lastSeparator + 2)) != -1)
            {
                var ret = path.Substring(0, lastSeparator);
                if (ret != "") yield return ret;
            }
        }

        static string GeneratePath(string key)
        {
            if (key.Length < 6)
                throw new ArgumentException(key);

            // convert files of the form abcdefghijk to ab/cd/abcdefjhijk
            // to reduce the number of files per directory.
            return string.Create(key.Length + 6, key, (chars, key) =>
            {
                chars[0] = key[0];
                chars[1] = key[1];
                chars[2] = '/';
                chars[3] = key[2];
                chars[4] = key[3];
                chars[5] = '/';

                key.AsSpan().CopyTo(chars.Slice(6));
            });
        }
        bool TryDownload(string path, out byte[] bytes);

        bool Upload(string path, byte[] content, OverwriteStrategy overwrite);

        bool Exists(string path);

        IEnumerable<string> ListFiles(string path);

        bool IChunkStorage.Exists(Address address)
        {
            var path = GeneratePath(address.ToString());

            return Exists(path);
        }

        IEnumerable<Address> IChunkStorage.ListAddresses()
        {
            foreach (var path in ListFiles(""))
            {
                var dirs = path.Split('/');
                if (dirs.Length == 3)
                {
                    if (dirs[0] == dirs[2].Substring(0, 2) && dirs[1] == dirs[2].Substring(2, 2))
                    {
                        if (dirs[2].Length == Address.Length * 2)
                        {
                            byte[]? bytes = null;
                            try
                            {
                                bytes = Convert.FromHexString(dirs[2]);
                            }
                            catch (Exception) { }

                            if (bytes != null) yield return new Address(bytes);
                        }
                    }
                }
            }
        }

        bool IChunkStorage.TryPut(EncryptedChunk chunk, OverwriteStrategy overwrite)
        {
            return Upload(GeneratePath(chunk.Address.ToString()), chunk.Content, overwrite);
        }

        bool IChunkStorage.TryGet(Address address, out EncryptedChunk chunk)
        {
            var path = GeneratePath(address.ToString());

            if (!TryDownload(path, out var bytes))
            {
                chunk = default;
                return false;
            }
             
            chunk = new EncryptedChunk(address, bytes);
            return true;
        }
    }
}