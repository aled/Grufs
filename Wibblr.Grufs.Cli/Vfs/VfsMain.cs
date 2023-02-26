using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Wibblr.Grufs.Core;
using Wibblr.Grufs.Filesystem;
using Wibblr.Grufs.Logging;

namespace Wibblr.Grufs.Cli
{
    public class VfsMain
    {
        private VfsArgs _args = new VfsArgs();

        public static void Main(string[] args)
        {
            Environment.Exit(new VfsMain().Run(args));
        }

        public int Run(string[] args)
        {
            var startTime = DateTime.UtcNow;

            var argDefinitions = new ArgDefinition[]
            {
                new ArgDefinition(null, "upload", x => _args.Upload = bool.Parse(x), isFlag: true),
                new ArgDefinition(null, "list", x => _args.List = bool.Parse(x), isFlag: true),
                new ArgDefinition(null, "download", x => _args.Download = bool.Parse(x), isFlag: true),
                new ArgDefinition('c', "config-dir", x => _args.ConfigDir = x),
                new ArgDefinition('n', "repo-name",  x => _args.RepoName = x),
                new ArgDefinition('d', "delete", x => _args.Delete = bool.Parse(x), isFlag: true),
                new ArgDefinition('r', "recursive", x => _args.Recursive = bool.Parse(x), isFlag: true),
                new ArgDefinition('f', "file-only", x => _args.FileOnly = bool.Parse(x), isFlag: true),
                new ArgDefinition(null, "local-path",  x => _args.LocalPath = x),
                new ArgDefinition(null, "vfs-path",  x => _args.VfsPath = x),
            };

            new ArgParser(argDefinitions).Parse(args);

            // Open repository
            if (_args.RepoName == null)
            {
                throw new UsageException("Repository name not specified");
            }
            Log.WriteLine(0, $"Repository name: '{_args.RepoName}'");

            if (_args.ConfigDir == null)
            {
                _args.ConfigDir = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".grufs");
            }
            Log.WriteLine(0, $"Config directory: '{_args.ConfigDir}'");

            var repoRegistryPath = Path.Join(_args.ConfigDir, "repos", _args.RepoName);

            var serialized = File.ReadAllText(repoRegistryPath);

            var repo = new RepositorySerializer().Deserialize(serialized);

            var result = repo.Open();

            switch (result.Status)
            {
                case OpenRepositoryStatus.Success:
                    Log.WriteLine(0, "Opened repository");
                    break;

                default:
                    Log.WriteLine(0, "Failed to open repository");
                    return -1;
            }

            // TODO: implement simultaneous upload and download
            try
            {
                if (_args.Upload)
                {
                    return Upload(repo);
                }
                if (_args.Download)
                {
                    return Download(repo);
                }
                if (_args.List)
                {
                    return List(repo, "[default]");
                }
            }
            finally
            {
                var endTime = DateTime.UtcNow;
                Log.WriteLine(0, $"Completed in {Math.Round(Convert.ToDecimal((endTime - startTime).TotalSeconds), 3)}s");
            }

            throw new UsageException("Operation not specified (--upload --download --list)");
        }

        public int Upload(Repository repo)
        {
            if (_args.LocalPath == null)
            {
                throw new UsageException("Local path not specified (--local-path c:\\temp)");
            }
            if (_args.VfsPath == null)
            {
                throw new UsageException("Vfs path not specified (--local-path /path/to/dir)");
            }

            if (_args.Recursive)
            {
                var (_, _, stats) = new VirtualFilesystem(repo, "[default]").UploadDirectoryRecursive(_args.LocalPath, new DirectoryPath(_args.VfsPath));
                Log.WriteLine(0, stats.ToString());
            }
            else
            {
                new VirtualFilesystem(repo, "[default]").UploadDirectoryNonRecursive(_args.LocalPath, new DirectoryPath(_args.VfsPath));
            }
            return 0;
        }

        public int List(Repository repo, string vfsName)
        {
            new VirtualFilesystem(repo, vfsName).ListDirectory(new DirectoryPath("/"));
            return 0;
        }

        public int Download(Repository repo)
        {
            if (_args.LocalPath == null)
            {
                throw new UsageException("Local path not specified (--local-path c:\\temp)");
            }
            if (_args.VfsPath == null)
            {
                throw new UsageException("Vfs path not specified (--local-path /path/to/dir)");
            }

            // TODO: don't require upfront arg to specify file or directory - discover at runtime
            if (_args.FileOnly)
            {
                var separatorIndex = _args.VfsPath.LastIndexOfAny(new[] { '/', '\\' });
                var virtualFile = _args.VfsPath.Substring(separatorIndex);
                var virtualDirectory = _args.VfsPath.Substring(0, separatorIndex);

                new VirtualFilesystem(repo, "[default]").Download(new DirectoryPath(virtualDirectory), new Filename(virtualFile), _args.LocalPath, false);
            }
            else
            {
                var recursive = _args.Recursive;

                new VirtualFilesystem(repo, "[default]").Download(new DirectoryPath(_args.VfsPath), null, _args.LocalPath, recursive);
            }
            return 0;
        }
    }
}
