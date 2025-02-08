using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using Wibblr.Grufs.Core;
using Wibblr.Grufs.Logging;
using Wibblr.Grufs.Storage;
using Wibblr.Grufs.Storage.Server;
using Wibblr.Grufs.Storage.Sftp;
using Wibblr.Grufs.Storage.Sqlite;

namespace Wibblr.Grufs.Cli
{
    public class RepoMain
    {
        private RepoArgs _repoArgs = new RepoArgs();

        public static async Task Main(string[] args)
        {
            Environment.Exit(await new RepoMain().RunAsync(args, CancellationToken.None));
        }

        public async Task<int> RunAsync(string[] args, CancellationToken token)
        {
            var argDefinitions = new ArgDefinition[]
            {
            new PositionalStringArgDefinition(0, x => {
                _repoArgs.Operation = x switch
                {
                    "init" => RepoArgs.OperationEnum.Init,
                    "register" => RepoArgs.OperationEnum.Register,
                    "unregister" => RepoArgs.OperationEnum.Unregister,
                    "list" => RepoArgs.OperationEnum.List,
                    "scrub" => RepoArgs.OperationEnum.Scrub,
                    _ => throw new UsageException()
                };
            }),
            new NamedStringArgDefinition('c', "config-dir", x => _repoArgs.ConfigDir = x),
            new NamedStringArgDefinition('n', "name",  x => _repoArgs.RepoName = x),
            new NamedFlagArgDefinition('o', "non-interactive", x => _repoArgs.NonInteractive = x),
            new NamedStringArgDefinition('U', "username", x => _repoArgs.Username = x),
            new NamedStringArgDefinition('P', "password", x => _repoArgs.Password = x),
            new NamedStringArgDefinition('e', "encryption-password", x => _repoArgs.EncryptionPassword = x),
            new PositionalStringArgDefinition(-1, x => _repoArgs.Location = x),
            };

            new ArgParser(argDefinitions).Parse(args);

            _repoArgs.ConfigDir ??= Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".grufs");
            //Log.WriteLine(0, $"Using config directory: '{_repoArgs.ConfigDir}'");

            var repoRegistrationDirectory = Path.Join(_repoArgs.ConfigDir, "repos");
            if (!Directory.Exists(repoRegistrationDirectory))
            {
                Directory.CreateDirectory(repoRegistrationDirectory);
                //Log.WriteLine(0, $"Created directory {repoRegistrationDirectory}");
            }

            if (_repoArgs.Operation == RepoArgs.OperationEnum.None)
            {
                throw new UsageException("Operation not specified (examples: init register unregister list scrub");
            }


            if (_repoArgs.RepoName == null)
            {
                Console.WriteLine("Repository name not specified, using default");
                _repoArgs.RepoName = "default";
            }

            string repoRegistrationPath = Path.Join(repoRegistrationDirectory, _repoArgs.RepoName);

            return _repoArgs.Operation switch
            {
                // TODO:
                // Sanitize the repo name
                RepoArgs.OperationEnum.Init => await InitAsync(repoRegistrationPath, CancellationToken.None),
                RepoArgs.OperationEnum.Register => await RegisterAsync(repoRegistrationPath, CancellationToken.None),
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
                throw new RepositoryException($"Repository '{_repoArgs.RepoName}' already registered");
            }

            if (_repoArgs.Location == null)
            {
                throw new UsageException("Location not specified (example: c:\\temp\\grufs\\myrepo)");
            }

            if (_repoArgs.EncryptionPassword == null)
            {
                if (_repoArgs.NonInteractive)
                {
                    throw new UsageException("Encryption password not specified (example: -e correct-horse-battery-staple)");
                }

                // Prompt for password
                Console.WriteLine("Enter encryption password, or <Enter> to automatically generate");
                Console.WriteLine($"This password will be stored in plaintext in the config directory '{_repoArgs.ConfigDir}'");
                _repoArgs.EncryptionPassword = Console.ReadLine() ?? "";

                if (!string.IsNullOrEmpty(_repoArgs.EncryptionPassword))
                {
                    Console.WriteLine("Re-enter encryption password");
                    var input2 = Console.ReadLine();

                    if (input2 != _repoArgs.EncryptionPassword)
                    {
                        Console.WriteLine("Passwords do not match");
                        throw new Exception();
                    }
                }
                else
                {
                    var temp = new byte[32];
                    Random.Shared.NextBytes(temp);
                    _repoArgs.EncryptionPassword = Convert.ToHexString(temp);
                }
            }
            //Log.WriteLine(0, $"Using repository name: '{_repoArgs.RepoName}'");

            // The location implicitly specifies the protocol:
            //  sftp: sftp://host[:port]/basedir
            //  server: http[s]://host[:port]/basedir
            //  sqlite: sqlite://c:\temp\mydb.sqlite
            //  directory: /tmp/mystorage

            IChunkStorage Create(RepoArgs repoArgs)
            {

                // sftp
                var match = Regex.Match(repoArgs.Location, "sftp:\\/\\/(?<host>([^\\/:])+)(:(?<port>([0-9])+))?\\/(?<basedir>.*)");
                if (match.Success)
                {
                    return new SftpStorage(
                        new SftpCredentials
                        {
                            Host = match.Groups["host"].Value,
                            Port = Convert.ToInt32(match.Groups["port"].Value ?? "22"),
                            Password = repoArgs.Password ?? throw new UsageException("Password not specified"),
                            Username = repoArgs.Username ?? throw new UsageException("Username not specified"),
                            PrivateKey = ""
                        },
                        match.Groups["basedir"].Value);
                }

                // http
                match = Regex.Match(repoArgs.Location, "http([s]?):\\/\\/(?<host>([^\\/:])+)(:(?<port>([0-9])+))?\\/(?<basedir>.*)");
                if (match.Success)
                {
                    return new HttpStorage(
                        match.Groups["host"].Value,
                        Convert.ToInt32(match.Groups["port"].Value ?? "22"),
                        match.Groups["basedir"].Value);
                }

                // sqlite
                match = Regex.Match(repoArgs.Location, "sqlite:\\/\\/(?<location>.*)");
                if (match.Success)
                {
                    return new SqliteStorage(match.Groups["location"].Value);
                }

                // local directory
                return new LocalStorage(repoArgs.Location);
            }

            IChunkStorage storage = Create(_repoArgs);

            return new Repository(_repoArgs.RepoName, storage, _repoArgs.EncryptionPassword);
        }

        private async Task<int> InitAsync(string repoRegistrationPath, CancellationToken token)
        {
            var repo = CreateRepositoryFromRegistration(repoRegistrationPath);
            var (status, message) = await repo.InitializeAsync(token);
            
            switch(status)
            {
                case InitRepositoryStatus.Success:
                    var serialized = new RepositorySerializer().Serialize(repo);
                    File.WriteAllText(repoRegistrationPath, serialized);
                    Console.WriteLine($"Successfully initialized repository '{repo.Name}'");
                    return 0;

                case InitRepositoryStatus.AlreadyExists:
                    Log.WriteLine(0, message);
                    return 0;

                case InitRepositoryStatus.Error:
                    Log.WriteLine(0, message);
                    return -1;

                default:
                    throw new Exception();
            }
        }

        private async Task<int> RegisterAsync(string repoRegistrationPath, CancellationToken token)
        {
            var repo = CreateRepositoryFromRegistration(repoRegistrationPath);
            var (status, message) = await repo.OpenAsync(token);

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
            }
            return 0;
        }

        private int Scrub(string repoRegistrationPath)
        {
            throw new NotImplementedException();
        }
    }
}