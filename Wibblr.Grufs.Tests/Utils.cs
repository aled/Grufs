namespace Wibblr.Grufs.Tests
{
    public static class Utils
    {
        public static string GetFileContent(string path)
        {
            return $"This is the content of file {path}";
        }

        public static void CreateDirectoryTree(string baseDir, params string[] paths)
        {
            Directory.CreateDirectory(baseDir);

            foreach (var path in paths)
            {
                var nativeFullPath = path.Replace('/', Path.DirectorySeparatorChar);
                var lastDirectorySeparatorIndex = nativeFullPath.LastIndexOf(Path.DirectorySeparatorChar);

                if (lastDirectorySeparatorIndex > 0)
                {
                    Directory.CreateDirectory(Path.Join(baseDir, nativeFullPath.Substring(0, lastDirectorySeparatorIndex)));
                }

                if (lastDirectorySeparatorIndex < path.Length)
                {
                    File.WriteAllText(Path.Join(baseDir, nativeFullPath), $"This is the content of file {path}");
                }
            }
        }
    }
}
