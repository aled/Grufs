using System;
using System.Text;

using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Cli
{
    public class RepoMain
    {
        private RepoConfig _config = new RepoConfig();

        public static void Main(string[] args)
        {
            Environment.Exit(new RepoMain().Run(args));
        }

        public int Run(string[] args)
        {
            var argDefinitions = new ArgDefinition[]
            {
                new ArgDefinition('i', "init", x => _config.Init = bool.Parse(x), isFlag: true),
                new ArgDefinition('c', "config-dir", x => _config.ConfigDir = x),
                new ArgDefinition('n', "name",  x => _config.RepoName = x),
                new ArgDefinition('o', "non-interactive", x => _config.NonInteractive = bool.Parse(x), isFlag: true),
                new ArgDefinition('p', "protocol", x => _config.Protocol = x),
                new ArgDefinition('t', "port", x => _config.Port = int.Parse(x)),
                new ArgDefinition('U', "username", x => _config.Username = x),
                new ArgDefinition('P', "password", x => _config.Password = x),
                new ArgDefinition('e', "encryption-password", x => _config.EncryptionPassword = x),
                new ArgDefinition('b', "basedir", x => _config.BaseDir = x),
            };

            new ArgParser(argDefinitions).Parse(args);

            if (_config.Init ?? false)
            {
                return Init();
            }

            throw new UsageException("Operation not specified (must be init)");
        }

        private int Init() 
        {
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
            if (!Directory.Exists(reposRegistry))
            {
                Directory.CreateDirectory(reposRegistry);
                Console.WriteLine($"Created directory {reposRegistry}");
            }

            // TODO:
            // Sanitize the repo name

            var repoConfigPath = Path.Join(reposRegistry, _config.RepoName);
            if (File.Exists(repoConfigPath))
            {
                Console.WriteLine($"Repository '{_config.RepoName}' already registered");
                return -1;
            }

            if (_config.Protocol == null)
            {
                _config.Protocol = "directory";
                Console.WriteLine($"Protocol: '{_config.Protocol}'");
            }

            if (_config.BaseDir == null)
            {
                throw new UsageException("Basedir not specified");
            }

            if (_config.EncryptionPassword == null)
            {
                throw new UsageException("Encryption password not specified");

                // TODO: Prompt for password, unless non-interactive is set
            }

            switch (_config.Protocol)
            {
                case "sftp":
                    {
                        if (_config.Username == null)
                        {
                            throw new UsageException("Username not specified");
                        }
                        if (_config.Password == null)
                        {
                            throw new UsageException("Password not specified");
                        }
                        if (_config.Host == null)
                        {
                            throw new UsageException("Host not specified");
                        }
                        if (_config.Port == null)
                        {
                            _config.Port = 22;
                        }

                        SftpStorage storage = new SftpStorage(_config.Host, _config.Port.Value, _config.Username, _config.Password, _config.BaseDir);

                        try
                        {
                            storage.EnsureConnected();
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("Unable to connect to repository");
                            return -1;
                        }

                        var repo = new Repository(storage);

                        if (repo.Initialize(_config.EncryptionPassword))
                        {
                            // write to local config
                            File.WriteAllText(
                                repoConfigPath,
                                $"repoName{_config.RepoName}\n" +
                                $"protocol:{_config.Protocol}\n" +
                                $"host:{_config.Host}\n" +
                                $"port:{_config.Port}\n" +
                                $"user:{_config.Username}\n" +
                                $"password:0x{Convert.ToHexString(Encoding.Unicode.GetBytes(_config.Password))}\n" +
                                $"baseDir:{_config.BaseDir}\n" +
                                $"encryptionPassword:0x{_config.EncryptionPassword}\n");

                            Console.WriteLine("Storage initialized");
                            return 0;
                        }
                    }
                    break;

                case "directory":
                    {
                        DirectoryStorage storage = new DirectoryStorage(_config.BaseDir);
                        var repo = new Repository(storage);

                        if (repo.Initialize(_config.EncryptionPassword))
                        {
                            // write to local config
                            File.WriteAllText(
                                repoConfigPath,
                                $"repoName:{_config.RepoName}\n" +
                                $"protocol:{_config.Protocol}\n" +
                                $"baseDir:{_config.BaseDir}\n" +
                                $"encryptionPassword:{_config.EncryptionPassword}\n");

                            Console.WriteLine("Storage initialized");
                            return 0;
                        }
                    }
                    break;

                default:
                    throw new UsageException("Storage type must be 'sftp' or 'directory'");
            }

            return -1;
        }
    }
}