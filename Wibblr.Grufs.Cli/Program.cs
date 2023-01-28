using System;
using System.Text.Json;

using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Cli
{
    /// <summary>
    /// Grand Remote Unified File System
    ///
    /// Stores encrypted and deduplicated data on a local or remote disk. The data cannot be decrypted by the server. Currently only SFTP is implemented.
    /// Designed to allow the following scenarios:
    ///
    ///   - Backup   Can be used to securely store backups of a local filesystem. These backups are immutable.
    ///              Works similarly to restic/tarsnap/borg, but usable on Windows.
    ///   
    ///   - Sync     Individual files or directories can be synced (uploaded and downloaded) to a virtual filesystem on the remote storage. Multiple clients can use the same 
    ///              virtual filesystem. Importantly, files are deduplicated regardless of whether they are backed up or synced.
    ///              It is possible to retrieve all previous versions of a synced file or directory.
    ///
    /// Usage:
    ///   - grufs.exe command subcommand [options]
    ///       where command is repo, backup, restore, sync
    ///
    ///  - repo subcommands:
    ///      init, register, unregister, list, scrub
    ///
    ///      - grufs.exe repo init --name myrepo --protocol sftp --host hostname --port port --user user --storage-password password --identity mykey.rsa --basedir ~/grufs-storage/repo1 --encryption-password password
    ///      - grufs.exe repo init --name myrepo ~/grufs-storage/repo1
    ///   
    ///      This will:
    ///        - create the directory on the remote server
    ///        - login to sftp if using public key authentication
    ///        - prompt for ssh password if required
    ///        - prompt for storage password if required
    ///        - create the metadata for the default repository
    ///        - show error if metadata already exists and password is incorrect
    ///        - update local config with 'mystorage = sftp://user@hostname:port/grufs-storage'
    ///
    ///       - grufs.exe repo list --verbose
    /// 
    ///       This will:
    ///         - list all repositories previously created
    ///         - the verbose flag will include: creation date, list of backupsets.
    ///
    ///       - grufs.exe repo scrub
    ///
    ///       This will:
    ///       - download all information from storage
    ///
    ///  - backup/restore subcommands
    ///    -- grufs.exe backup mybackup --storage mystorage  --directory 'c:\my documents' 
    ///    -- grufs.exe backup mybackup
    ///    -- grufs.exe restore mybackup --include **/*.mp3 --destination c:\my-restore-dir 
    ///
    ///  - sync subcommands
    ///    -- grufs.exe sync [--list-versions] [--upload] [--download] [--no-recursive] [--no-delete] [--trust-storage] c:\mydirectory my-storage:my-virtual-filesystem:some/other/directory
    ///
    /// </summary>
    /// 
    internal partial class Program
    {
        static string LocalConfigDirectory => Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".grufs");

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("usage:  grufs.exe command subcommand [options]"); 
                Console.WriteLine("where command is repo, backup, restore, sync, interactive");
                return;

            }
            if (args[0] == "interactive")
            {
                Interactive();
            }
            else if (args[0] == "storage")
            {
                Repository(args.Skip(1).ToArray());
            }
        }

        static List<KeyValuePair<string, string>> ParseArgs(string[] args, params string[] optionKeys)
        {
            var options = new List<KeyValuePair<string, string>>();
            var invalidOptionKeys = new List<string>();
            
            string? currentOptionKey = null;
            foreach (var arg in args)
            {
                if (arg.StartsWith("--") || currentOptionKey == null)
                {
                    currentOptionKey = arg.Substring(2);
                }
                else
                {
                    if (optionKeys.Contains(currentOptionKey))
                    {
                        options.Add(new KeyValuePair<string, string>(currentOptionKey, arg));
                    }
                    else
                    {
                        invalidOptionKeys.Add(currentOptionKey);
                    }
                }
            }

            if (invalidOptionKeys.Any())
            {
                Console.WriteLine($"Warning: unknown options {string.Join(',', invalidOptionKeys)}");
            }
            return options;
        }

        static string ReadFromConsole(string prompt, string defaultValue)
        {
            Console.Write($"{prompt} [{defaultValue}]: ");
            var line = Console.ReadLine();
            return string.IsNullOrEmpty(line) ? defaultValue : line;
        }

        static string ExtractOrRead(List<KeyValuePair<string, string>> options, string optionKey, string prompt, string defaultValue)
        {
            return options.FirstOrDefault(x => x.Key == optionKey).Value ?? ReadFromConsole(prompt, defaultValue);
        }

        static void Interactive()
        { 
            var json = File.ReadAllText(Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".grufs", "sftp-credentials.json"));

            // Use this overload of Deserialize() to enable native AOT compilation
            var sftpCredentials = JsonSerializer.Deserialize(json, SourceGenerationContext.Default.SftpCredentials) ?? throw new Exception("Error deserializing SFTP credentials");

            if (string.IsNullOrEmpty(sftpCredentials.Password))
            {
                Console.WriteLine("Enter SFTP password");
                sftpCredentials.Password = Console.ReadLine();
            }

            var storage = new SftpStorage(
                    sftpCredentials.Hostname ?? throw new Exception("Invalid SFTP hostname"),
                    22,
                    sftpCredentials.Username ?? throw new Exception("Invalid SFTP username"),
                    sftpCredentials.Password ?? throw new Exception("Invalid SFTP password"));

            storage.EnsureConnected();
            Console.WriteLine("Connected");

            Console.WriteLine("Enter storage base dir");
            storage.WithBaseDir(Console.ReadLine() ?? throw new Exception());

            var repo = new Repository(storage);

            Console.WriteLine("Enter encryption password: ");
            var password = Console.ReadLine() ?? throw new Exception();

            if (storage.Exists())
            {
                var result = repo.Open(password);
                switch (result.Status)   
                {
                    case OpenRepositoryStatus.Success:
                        Console.WriteLine("Opened repository");
                        break;

                    default:
                        repo.Initialize(password);
                        Console.WriteLine("Initialized repository");
                        break;
                }
            }
            else
            {
                repo.Initialize(password);
                Console.WriteLine("Initialized repository");
            }

            while (true)
            {
                Console.WriteLine("1. Upload directory (non recursive)");
                Console.WriteLine("2. Upload directory (recursive)");
                Console.WriteLine("3. List all syncable files");
                Console.WriteLine("4. Download file");
                Console.WriteLine("Q. Quit");
                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        {
                            Console.Write($"Enter local directory path");
                            var localPath = Console.ReadLine() ?? throw new Exception();

                            if (!Directory.Exists(localPath))
                            {
                                Console.WriteLine($"No such directory: {localPath}");
                            }
                            else
                            {
                                Console.Write($"Enter remote directory path");
                                var remotePath = Console.ReadLine() ?? throw new Exception();

                                Console.WriteLine($"Uploading {localPath} to {remotePath}");

                                new MutableFilesystem(repo, "[default]").UploadDirectoryNonRecursive(localPath, new DirectoryPath(remotePath));
                            }
                        }
                        break;

                    case "2":
                        {
                            Console.WriteLine($"Enter local directory path");
                            var localPath = Console.ReadLine() ?? throw new Exception();

                            if (!Directory.Exists(localPath))
                            {
                                Console.WriteLine($"No such directory: {localPath}");
                            }
                            else
                            {
                                Console.WriteLine($"Enter remote directory path");
                                var remotePath = Console.ReadLine() ?? throw new Exception();

                                Console.WriteLine($"Uploading {localPath} to {remotePath}");

                                new MutableFilesystem(repo, "[default]").UploadDirectoryRecursive(localPath, new DirectoryPath(remotePath));
                            }
                        }
                        break;

                    case "3":
                        new MutableFilesystem(repo, "[default]").ListDirectory(new DirectoryPath("/"));
                        break;

                    case "4":
                        {
                            Console.Write("Enter full path of remote file: ");
                            var remotePath = Console.ReadLine() ?? throw new Exception();
                            var remoteDir = new DirectoryPath(remotePath.Substring(0, remotePath.LastIndexOf("/")));
                            var remoteFile = new Filename(remotePath.Substring(remotePath.LastIndexOf("/") + 1));

                            Console.Write("Enter local directory for restore: ");
                            var localDirectory = Console.ReadLine() ?? throw new Exception();

                            new MutableFilesystem(repo, "[default]").DownloadFile(remoteDir, remoteFile, localDirectory);
                        }
                        break;

                    case "Q":
                    case "q":
                        break;

                    default:
                        break;
                }
            }
        }
    }
}