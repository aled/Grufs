using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wibblr.Grufs.Storage
{
    public class DirectoryStorage : AbstractFileStorage
    {
        public DirectoryStorage(string baseDir)
            : base(baseDir, Path.DirectorySeparatorChar)
        {
        }

        public override CreateDirectoryStatus CreateDirectory(string relativePath)
        {
            try
            {
                var path = Path.Join(BaseDir, relativePath);
                var result = Directory.CreateDirectory(path);
                return CreateDirectoryStatus.Success;
            }
            catch (Exception)
            {
                return CreateDirectoryStatus.UnknownError;
            }

        }

        public override void DeleteDirectory(string relativePath)
        {
            try
            {
                var path = Path.Join(BaseDir, relativePath);
                Directory.Delete(path, true);
            }
            catch (Exception e)
            {
                //[x]
                Console.WriteLine(e.Message);
            }
        }

        public override bool Exists(string relativePath)
        {
            var path = Path.Join(BaseDir, relativePath);
            //[x]
            return File.Exists(path);
        }

        public override (List<string> files, List<string> directories) ListDirectoryEntries(string relativePath)
        {
            try
            {
                var path = Path.Join(BaseDir, relativePath);
                return (
                    Directory.GetFiles(path).Select(x => new FileInfo(x).Name).ToList(), 
                    Directory.GetDirectories(path).Select(x => new DirectoryInfo(x).Name).ToList());
            }
            catch (Exception e)
            {
                //[x]
                Console.WriteLine(e.Message);
                throw;
            }
        }

        override public ReadFileStatus ReadFile(string relativePath, out byte[] bytes)
        {
            try
            {
                var path = Path.Join(BaseDir, relativePath);
                bytes = File.ReadAllBytes(path);
                return ReadFileStatus.Success;
            }
            catch (Exception e)
            {
                //TODO: error handling
                Console.WriteLine(e.Message);
                bytes = new byte[0];
                return ReadFileStatus.UnknownError;
            }
        }

        public override WriteFileStatus WriteFile(string relativePath, byte[] content, OverwriteStrategy overwrite)
        {
            try
            {
                var path = Path.Join(BaseDir, relativePath);

                switch (overwrite)
                {
                    case OverwriteStrategy.Allow:
                        File.WriteAllBytes(path, content);
                        return WriteFileStatus.Success;

                    case OverwriteStrategy.Deny:
                        if (File.Exists(path))
                        {
                            return WriteFileStatus.OverwriteDenied;
                        }
                        break;
                }

                File.WriteAllBytes(path, content);
                return WriteFileStatus.Success;
            }
            catch (DirectoryNotFoundException)
            {
                return WriteFileStatus.PathNotFound;
            }
            catch (Exception e)
            {
                //TODO: error handling
                Console.WriteLine(e.Message);
                return WriteFileStatus.Error;
            }
        }
    }
}
