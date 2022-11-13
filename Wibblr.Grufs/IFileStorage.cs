namespace Wibblr.Grufs
{
    public interface IFileStorage : IStorage
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
            if (key.Length < 3)
                throw new ArgumentException(key);

            // convert abcdef to a/b/c/def
            return string.Create(key.Length + 3, key, (chars, key) =>
            {
                chars[0] = key[0];
                chars[1] = '/';
                chars[3] = key[1];
                chars[4] = '/';
                chars[7] = key[2];
                chars[8] = '/';

                key.AsSpan(3, key.Length - 3).CopyTo(chars.Slice(6));
            });
        }
        byte[] Download(string path);

        void Upload(string path, byte[] content);

        byte[] IStorage.Read(string key)
        {
            return Download(GeneratePath(key));
        }

         void IStorage.Write(string key, byte[] content)
        {
            Upload(GeneratePath(key), content);
        }
    }
}