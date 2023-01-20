using System.Text.Json;

using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs.Cli
{
    /// <summary>
    /// Usage:
    ///   - grufs.exe command subcommand [options]
    ///       where command is storage, backup, restore
    /// 
    ///  - storage subcommands:
    ///      init, refresh, list
    ///       
    ///      - grufs.exe storage init --name mystorage --protocol sftp --host hostname --port port --user user --storage-password password --identity mykey.rsa --basedir ~/grufs-storage --encryption-password password
    ///      - grufs.exe storage init --name mystorage ~/grufs-storage
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
    ///       - grufs.exe storage list --verbose
    /// 
    ///       This will:
    ///         - list all storages previously created
    ///         - the verbose flag will include: creation date, list of backupsets.
    ///     
    ///       - grufs.exe storage refresh --protocol sftp ...
    ///       
    ///       This will:
    ///       - download all information from storage
    ///       
    ///  - backup subcommands
    ///    -- grufs.exe backup mybackup --storage mystorage  --directory 'c:\my documents' 
    ///    -- grufs.exe backup mybackup
    /// 
    /// </summary>
    /// 

    class Arguments
    {
        //public string 
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            var json = File.ReadAllText(Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".grufs", "sftp-credentials.json"));

            // Use this overload of Deserialize() to enable native AOT compilation
            var sftpCredentials = JsonSerializer.Deserialize(json, SourceGenerationContext.Default.SftpCredentials) ?? throw new Exception("Error deserializing SFTP credentials");

            if (string.IsNullOrEmpty(sftpCredentials.Password))
            {
                Console.WriteLine("Enter SFTP password");
                sftpCredentials.Password = Console.ReadLine();
            }

            var storage = (SftpStorage)new SftpStorage(
                    sftpCredentials.Hostname ?? throw new Exception("Invalid SFTP hostname"),
                    sftpCredentials.Username ?? throw new Exception("Invalid SFTP username"),
                    sftpCredentials.Password ?? throw new Exception("Invalid SFTP password"));

            storage.EnsureConnected();
            Console.WriteLine("Connected");

            Console.WriteLine("Enter base dir");
            storage.WithBaseDir(Console.ReadLine() ?? throw new Exception());


            var repo = new Repository(storage);

            Console.WriteLine("Enter encryption password: ");
            var password = Console.ReadLine() ?? throw new Exception();

            if (storage.Exists())
            {
                if (repo.Open(password))
                {
                    Console.WriteLine("Opened repository");
                }
                else
                {
                    repo.Initialize(password);
                    Console.WriteLine("Initialized repository");
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
                Console.WriteLine("3. List all directories");
                Console.WriteLine("Q. Quit");
                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
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