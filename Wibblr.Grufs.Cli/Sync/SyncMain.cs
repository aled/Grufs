using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Wibblr.Grufs.Cli.Sync;
using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Cli
{
    public class SyncMain
    {
        private SyncConfig _config = new SyncConfig();

        public static void Main(string[] args)
        {
            Environment.Exit(new SyncMain().Run(args));
        }

        public int Run(string[] args)
        {
            var argDefinitions = new ArgDefinition[]
            {
                new ArgDefinition('u', "upload", x => _config.Upload = bool.Parse(x), isFlag: true),
                new ArgDefinition('d', "download", x => _config.Download = bool.Parse(x), isFlag: true),
                new ArgDefinition('c', "config-dir", x => _config.ConfigDir = x),
                new ArgDefinition('s', "repo",  x => _config.RepoName = x),
                new ArgDefinition('e', "delete", x => _config.Delete = bool.Parse(x), isFlag: true),
                new ArgDefinition('r', "recursive", x => _config.Recursive = bool.Parse(x), isFlag: true),
                new ArgDefinition('f', "file", x => _config.FileOnly = bool.Parse(x), isFlag: true),
                new ArgDefinition('l', "local",  x => _config.LocalPath = x),
                new ArgDefinition('v', "virtual",  x => _config.VirtualPath = x),
            };

            new ArgParser(argDefinitions).Parse(args);

            // Open repository
            if (_config.RepoName == null)
            {
                throw new UsageException("Repository name not specified");
            }
            Console.WriteLine($"Repository name: '{_config.RepoName}'");

            if (_config.ConfigDir == null)
            {
                _config.ConfigDir = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".grufs");
            }
            Console.WriteLine($"Config directory: '{_config.ConfigDir}'");

            var reposRegistry = Path.Join(_config.ConfigDir, "repos");

            var repoConfig = File.ReadAllLines(Path.Join(reposRegistry, _config.RepoName));

            var dict = repoConfig.Select(x => x.Split(":", 2))
                .ToDictionary(x => x[0], x => x[1]);

            IChunkStorage storage = dict["protocol"] switch
            {
                "sftp" => new SftpStorage(dict["host"], int.Parse(dict["port"]), dict["username"], dict["password"], dict["baseDir"]),
                "directory" => new DirectoryStorage(dict["baseDir"]),
                _ => throw new Exception("Protocol must be 'sftp' or 'directory'")
            };

            var repo = new Repository(storage);

            var result = repo.Open(dict["encryptionPassword"]);

            switch (result.Status)
            {
                case OpenRepositoryStatus.Success:
                    Console.WriteLine("Opened repository");
                    break;

                default:
                    Console.WriteLine("Failed to open repository");
                    return -1;
            }


            // TODO: implement simultaneous upload and download
            if (_config.Upload)
            {
                return Upload(repo);
            }
            if (_config.Download)
            {
                return Download(repo);
            }

            throw new UsageException("Operation not specified (must be init)");
        }

        public int Upload(Repository repo)
        {
            if (_config.Recursive)
            {
                new VirtualFilesystem(repo, "[default]").UploadDirectoryRecursive(_config.LocalPath, new DirectoryPath(_config.VirtualPath));
            }
            else
            {
                new VirtualFilesystem(repo, "[default]").UploadDirectoryNonRecursive(_config.LocalPath, new DirectoryPath(_config.VirtualPath));
            }
            return 0;
        }

        public int Download(Repository repo)
        {
            // TODO: don't require upfront arg to specify file or directory - discover at runtime
            if (_config.FileOnly)
            {
                var separatorIndex = _config.VirtualPath.LastIndexOfAny(new[] { '/', '\\' });
                var virtualFile = _config.VirtualPath.Substring(separatorIndex);
                var virtualDirectory = _config.VirtualPath.Substring(0, separatorIndex);

                new VirtualFilesystem(repo, "[default]").Download(new DirectoryPath(virtualDirectory), new Filename(virtualFile), _config.LocalPath);
            }
            else
            {
                new VirtualFilesystem(repo, "[default]").Download(new DirectoryPath(_config.VirtualPath), null, _config.LocalPath);
            }
            return 0;
        }
    }
}
