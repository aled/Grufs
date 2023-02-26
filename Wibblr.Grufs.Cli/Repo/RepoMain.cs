using System;

using Wibblr.Grufs.Core;
using Wibblr.Grufs.Logging;
using Wibblr.Grufs.Storage;
using Wibblr.Grufs.Storage.Sqlite;

namespace Wibblr.Grufs.Cli
{
    public class RepoMain
    {
        private RepoArgs _repoArgs = new RepoArgs();

        public static void Main(string[] args)
        {
            Environment.Exit(new RepoMain().Run(args));
        }

        public int Run(string[] args)
        {
            var argDefinitions = new ArgDefinition[]
            {
                new ArgDefinition(null, "init", x => _repoArgs.Operation = RepoArgs.OperationEnum.Init, isFlag: true),
                new ArgDefinition(null, "register", x => _repoArgs.Operation = RepoArgs.OperationEnum.Register, isFlag: true),
                new ArgDefinition(null, "unregister", x => _repoArgs.Operation = RepoArgs.OperationEnum.Unregister, isFlag: true),
                new ArgDefinition(null, "list", x => _repoArgs.Operation = RepoArgs.OperationEnum.List, isFlag: true),
                new ArgDefinition(null, "scrub", x => _repoArgs.Operation = RepoArgs.OperationEnum.Scrub, isFlag: true),
                new ArgDefinition('c', "config-dir", x => _repoArgs.ConfigDir = x),
                new ArgDefinition('n', "name",  x => _repoArgs.RepoName = x),
                new ArgDefinition('o', "non-interactive", x => _repoArgs.NonInteractive = bool.Parse(x), isFlag: true),
                new ArgDefinition('p', "protocol", x => _repoArgs.Protocol = x),
                new ArgDefinition('h', "host", x => _repoArgs.Host = x),
                new ArgDefinition('t', "port", x => _repoArgs.Port = int.Parse(x)),
                new ArgDefinition('U', "username", x => _repoArgs.Username = x),
                new ArgDefinition('P', "password", x => _repoArgs.Password = x),
                new ArgDefinition('e', "encryption-password", x => _repoArgs.EncryptionPassword = x),
                new ArgDefinition('b', "basedir", x => _repoArgs.BaseDir = x),
            };

            new ArgParser(argDefinitions).Parse(args);

            _repoArgs.ConfigDir ??= Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".grufs");
            Log.WriteLine(0, $"Using config directory: '{_repoArgs.ConfigDir}'");

            var repoRegistrationDirectory = Path.Join(_repoArgs.ConfigDir, "repos");
            if (!Directory.Exists(repoRegistrationDirectory))
            {
                Directory.CreateDirectory(repoRegistrationDirectory);
                //Log.WriteLine(0, $"Created directory {repoRegistrationDirectory}");
            }

            if (_repoArgs.Operation == RepoArgs.OperationEnum.None)
            {
                throw new UsageException("Operation not specified (examples: --init --register, --unregister --list --scrub");
            }

            string repoRegistrationPath = Path.Join(repoRegistrationDirectory, _repoArgs.RepoName);

            return _repoArgs.Operation switch
            {
                // TODO:
                // Sanitize the repo name
                RepoArgs.OperationEnum.Init => Init(repoRegistrationPath),
                RepoArgs.OperationEnum.Register => Register(repoRegistrationPath),
                RepoArgs.OperationEnum.Unregister => Unregister(repoRegistrationPath),
                RepoArgs.OperationEnum.List => ListRepos(repoRegistrationDirectory),
                RepoArgs.OperationEnum.Scrub => Scrub(repoRegistrationPath),
                _ => throw new UsageException("Invalid operation")
            };
        }

        private Repository CreateRepositoryFromRegistration(string repoRegistrationPath)
        {
            if (File.Exists(repoRegistrationPath))
            {
                Log.WriteLine(0, $"Repository '{_repoArgs.RepoName}' already registered");
                throw new Exception();
            }

            if (_repoArgs.Protocol == null)
            {
                _repoArgs.Protocol = "directory";
                Log.WriteLine(0, $"Using protocol: '{_repoArgs.Protocol}'");
            }

            if (_repoArgs.BaseDir == null)
            {
                throw new UsageException("Basedir not specified (example: -b c:\\temp\\grufs\\myrepo");
            }

            if (_repoArgs.EncryptionPassword == null)
            {
                throw new UsageException("Encryption password not specified (example: -e correct-horse-battery-staple");

                // TODO: Prompt for password, unless non-interactive is set
            }

            if (_repoArgs.RepoName == null)
            {
                throw new UsageException("Repository name not specified (example: -n myrepo)");
            }
            Log.WriteLine(0, $"Using repository name: '{_repoArgs.RepoName}'");

            IChunkStorage storage = _repoArgs.Protocol switch
            {
                "sqlite" => new SqliteStorage(
                            Path.Join(_repoArgs.BaseDir, _repoArgs.RepoName + ".sqlite")),

                "sftp" => new SftpStorage(
                            _repoArgs.Host ?? throw new UsageException("Host not specified"),
                            _repoArgs.Port ?? 22,
                            _repoArgs.Username ?? throw new UsageException("Username not specified"),
                            _repoArgs.Password ?? throw new UsageException("Password not specified"),
                            _repoArgs.BaseDir).EnsureConnected(),

                "directory" => new LocalStorage(_repoArgs.BaseDir),

                _ => throw new UsageException("Storage type must be 'sftp' or 'directory'")
            };

            return new Repository(_repoArgs.RepoName, storage, _repoArgs.EncryptionPassword);
        }

        private int Init(string repoRegistrationPath) 
        {
            var repo = CreateRepositoryFromRegistration(repoRegistrationPath);
            var (status, message) = repo.Initialize();
            
            switch(status)
            {
                case InitRepositoryStatus.Success:
                    var serialized = new RepositorySerializer().Serialize(repo);
                    File.WriteAllText(repoRegistrationPath, serialized);
                    return 0;

                case InitRepositoryStatus.AlreadyExists:
                    Log.WriteLine(0, message);
                    return 0;

                default:
                    throw new Exception();
            }
        }

        private int Register(string repoRegistrationPath)
        {
            var repo = CreateRepositoryFromRegistration(repoRegistrationPath);
            var (status, message) = repo.Open();

            switch (status)
            {
                case OpenRepositoryStatus.Success:
                    var serialized = new RepositorySerializer().Serialize(repo);
                    File.WriteAllText(repoRegistrationPath, serialized);
                    return 0;

                case OpenRepositoryStatus.MissingMetadata:
                case OpenRepositoryStatus.BadPassword:
                case OpenRepositoryStatus.InvalidMetadata:
                case OpenRepositoryStatus.Unknown:
                    Log.WriteLine(0, message);
                    return -1;

                default:
                    throw new Exception();
            }
        }

        private int Unregister(string repoRegistrationPath)
        {
            if (!File.Exists(repoRegistrationPath))
            {
                Log.WriteLine(0, $"Repository '{_repoArgs.RepoName}' is not registered");
                return -1;
            }

            if (Log.StdOutIsConsole)
            {
                Console.Write($"After deregistering this repository, you will need to re-enter the password to access it again. Type '{_repoArgs.RepoName}' to continue: ");
            }
            if (!Log.StdOutIsConsole || Console.ReadLine() == _repoArgs.RepoName)
            {
                File.Delete(repoRegistrationPath);
                return 0;
            }

            return -1;
        }

        private int ListRepos(string repoRegistrationDirectory)
        {
            Log.WriteLine(0, "Registered repositories:");
            foreach (var f in Directory.GetFiles(repoRegistrationDirectory))
            {
                var content = File.ReadAllText(f);
                var repo = new RepositorySerializer().Deserialize(content);
                Log.WriteLine(0, new FileInfo(f).Name);
            }
            return 0;
        }

        private int Scrub(string repoRegistrationPath)
        {
            throw new NotImplementedException();
        }
    }
}