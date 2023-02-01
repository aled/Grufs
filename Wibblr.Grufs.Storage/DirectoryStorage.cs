using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wibblr.Grufs
{
    public class DirectoryStorage : AbstractFileStorage
    {
        public DirectoryStorage(string baseDir)
            : base(baseDir, Path.DirectorySeparatorChar)
        {
        }

        public override CreateDirectoryResult CreateDirectory(string relativePath)
        {
            try
            {
                var path = Path.Join(_baseDir, relativePath);
                var result = Directory.CreateDirectory(path);
                return CreateDirectoryResult.Success;
            }
            catch (Exception)
            {
                return CreateDirectoryResult.UnknownError;
            }

        }

        public override void DeleteDirectory(string relativePath)
        {
            try
            {
                var path = Path.Join(_baseDir, relativePath);
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
            var path = Path.Join(_baseDir, relativePath);
            //[x]
            return File.Exists(path);
        }

        public override (List<string> files, List<string> directories) ListDirectoryEntries(string relativePath)
        {
            try
            {
                var path = Path.Join(_baseDir, relativePath);
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

        override public ReadFileResult ReadFile(string relativePath, out byte[] bytes)
        {
            try
            {
                var path = Path.Join(_baseDir, relativePath);
                bytes = File.ReadAllBytes(path);
                return ReadFileResult.Success;
            }
            catch (Exception e)
            {
                //TODO: error handling
                Console.WriteLine(e.Message);
                bytes = new byte[0];
                return ReadFileResult.UnknownError;
            }
        }

        public override WriteFileResult WriteFile(string relativePath, byte[] content, OverwriteStrategy overwrite)
        {
            try
            {
                var path = Path.Join(_baseDir, relativePath);

                switch (overwrite)
                {
                    case OverwriteStrategy.Allow:
                        File.WriteAllBytes(path, content);
                        return WriteFileResult.Success;

                    case OverwriteStrategy.DenyWithSuccess:
                        if (File.Exists(path))
                        {
                            return WriteFileResult.Success;
                        }
                        break;


                    case OverwriteStrategy.DenyWithError:
                        if (File.Exists(path))
                        {
                            return WriteFileResult.AlreadyExistsError;
                        }
                        break;
                }

                File.WriteAllBytes(path, content);
                return WriteFileResult.Success;
            }
            catch (DirectoryNotFoundException)
            {
                return WriteFileResult.PathNotFound;
            }
            catch (Exception e)
            {
                //TODO: error handling
                Console.WriteLine(e.Message);
                return WriteFileResult.UnknownError;
            }
        }
    }
}
