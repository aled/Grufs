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
                    "ls" => VfsArgs.OperationEnum.List,
                    "sync" =>  VfsArgs.OperationEnum.Sync,
                    _ => throw new UsageException()
                }),
                new NamedStringArgDefinition('c', "config-dir", x => _args.ConfigDir = x),
                new NamedStringArgDefinition('n', "repo-name",  x => _args.RepoName = x),
                new NamedFlagArgDefinition('d', "delete", x => _args.Delete = x),
                new NamedFlagArgDefinition('r', "recursive", x => _args.Recursive = x),
                new NamedFlagArgDefinition('p', "progress", x => _args.Progress = x),
                new NamedFlagArgDefinition('v', "verbose", x => _args.Verbose += x ? 1 : -1),
                new NamedFlagArgDefinition('h', "human", x => _args.Human = x),
                new PositionalStringArgDefinition(-2, x => _args.Source = x),
                new PositionalStringArgDefinition(-1, x => _args.Destination = x),
            };

            new ArgParser(argDefinitions).Parse(args);

            Log.HumanFormatting = _args.Human;
            Log.Verbose = _args.Verbose;
            Log.Progress = _args.Progress;

            // Open repository
            if (_args.RepoName == null)
            {
                throw new UsageException("Repository name not specified (example: -n myrepo)");
            }
            Log.WriteLine(1, $"Repository name: '{_args.RepoName}'");

            if (_args.ConfigDir == null)
            {
                _args.ConfigDir = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".grufs");
            }
            Log.WriteLine(1, $"Config directory: '{_args.ConfigDir}'");

            var repoRegistryPath = Path.Join(_args.ConfigDir, "repos", _args.RepoName);

            var serialized = File.ReadAllText(repoRegistryPath);

            var repo = new RepositorySerializer().Deserialize(serialized);

            var result = repo.Open();

            switch (result.Status)
            {
                case OpenRepositoryStatus.Success:
                    Log.WriteLine(1, "Opened repository");
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
                    return List(repo, new Timestamp(DateTime.MinValue), new Timestamp(DateTime.MaxValue));
                }
            }
            finally
            {
                var endTime = DateTime.UtcNow;
                Log.WriteLine(0, $"Completed in {Math.Round(Convert.ToDecimal((endTime - startTime).TotalSeconds), 3)}s");
            }

            throw new UsageException("Operation not specified (--sync or --ls)");
        }

        public int Sync(Repository repo)
        {
            if (_args.Source == null)
            {
                throw new UsageException("Source path(s) not specified");
            }
            if (_args.Destination == null)
            {
                throw new UsageException("Destination path not specified");
            }

            var vfs = new VirtualFilesystem(repo, "[default]");

            try
            {
                return vfs.Sync(_args.Source, _args.Destination, _args.Recursive);
            }
            catch (Exception) 
            {
                return -1;
            }
        }

        public int List(Repository repo, Timestamp from, Timestamp to)
        {
            if (_args.Destination == null)
            {
                throw new UsageException("Path not specified");
            }

            var vfs = new VirtualFilesystem(repo, "[default]");

            vfs.ListDirectoryRecursive(new DirectoryPath(_args.Destination));
            return 0;
        }
    }
}
