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
                new PositionalStringArgDefinition(0, x => _args.Operation = x switch
                {
                    "list" => VfsArgs.OperationEnum.List,
                    "sync" =>  VfsArgs.OperationEnum.Sync,
                    _ => throw new UsageException()
                }),
                new NamedStringArgDefinition('c', "config-dir", x => _args.ConfigDir = x),
                new NamedStringArgDefinition('n', "repo-name",  x => _args.RepoName = x),
                new NamedFlagArgDefinition('d', "delete", x => _args.Delete = x),
                new NamedFlagArgDefinition('r', "recursive", x => _args.Recursive = x),
                new NamedFlagArgDefinition('f', "file-only", x => _args.FileOnly = x),
                new NamedFlagArgDefinition('p', "progress", x => _args.Progress = x),
                new NamedFlagArgDefinition('v', "verbose", x => _args.Verbose += x ? 1 : -1),
                new PositionalStringArgDefinition(-2, x => _args.Source = x),
                new PositionalStringArgDefinition(-1, x => _args.Destination = x),
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
                if (_args.Operation == VfsArgs.OperationEnum.Sync)
                {
                    return Sync(repo);
                }
                if (_args.Operation == VfsArgs.OperationEnum.List)
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

        public int Sync(Repository repo)
        {
            if (_args.Source == null)
            {
                throw new UsageException("Source path not specified");
            }
            if (_args.Destination == null)
            {
                throw new UsageException("Destination path not specified");
            }

            // Either the source or destination must be a VFS path
            var vfsPrefix = "vfs://";
            var sourceIsVfs = _args.Source.StartsWith(vfsPrefix);
            var destIsVfs = _args.Destination.StartsWith(vfsPrefix);

            if (!(sourceIsVfs ^ destIsVfs))
            {
                throw new UsageException();
            }

            var vfs = new VirtualFilesystem(repo, "[default]");
            if (sourceIsVfs)
            {
                var vfsDirectory = new DirectoryPath(_args.Source.Substring(vfsPrefix.Length));
                var localDirectory = _args.Destination;

                vfs.Download(vfsDirectory, null, localDirectory, _args.Recursive);
            }
            else
            {
                var vfsDirectory = new DirectoryPath(_args.Destination.Substring(vfsPrefix.Length));
                var localDirectory = _args.Source;

                var (_, _, stats) = vfs.UploadDirectory(localDirectory, vfsDirectory, _args.Recursive);
                Log.WriteLine(0, stats.ToString());
            }

            return 0;
        }

        public int List(Repository repo, string vfsName)
        {
            new VirtualFilesystem(repo, vfsName).ListDirectory(new DirectoryPath("/"));
            return 0;
        }
    }
}
