using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs
{
    public interface IFileStorage : IChunkRepository
    {
        IEnumerable<string> GetParentDirectories(string path)
        {
            int lastSeparator = -1;
            while ((lastSeparator = path.IndexOf('/', lastSeparator + 1)) != -1)
            {
                yield return path.Substring(0, lastSeparator);
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
        byte[] Download(string path);

        bool Upload(string path, byte[] content, bool allowOverwrite);

        bool IChunkRepository.TryPut(EncryptedChunk chunk)
        {
            return Upload(GeneratePath(chunk.Address.ToString()), chunk.Content, false);
        }

        bool IChunkRepository.TryGet(Address address, out EncryptedChunk chunk)
        {
            chunk = new EncryptedChunk(address, Download(GeneratePath(address.ToString())));
            return true;
        }
    }
}