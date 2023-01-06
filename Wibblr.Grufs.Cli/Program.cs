using System.Reflection.Metadata.Ecma335;
using System.Text.Json;

using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs.Cli
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var json = File.ReadAllText(Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".grufs", "sftp-credentials.json"));

            // Use this overload of Deserialize() to enable native AOT compilation
            var sftpCredentials = JsonSerializer.Deserialize(json, SourceGenerationContext.Default.SftpCredentials) ?? throw new Exception("Error deserializing SFTP credentials");

            var storage = (SftpStorage) new SftpStorage(
                    sftpCredentials.Hostname ?? throw new Exception("Invalid SFTP hostname"),
                    sftpCredentials.Username ?? throw new Exception("Invalid SFTP username"),
                    sftpCredentials.Password ?? throw new Exception("Invalid SFTP password"))
                .WithBaseDir("grufs/repo1");

            storage.EnsureConnected();
            Console.WriteLine("Connected");

            var repo = new Repository(storage);

            Console.WriteLine("Enter password: ");
            var password = Console.ReadLine() ?? throw new Exception();

            if (storage.Exists())
            {
                repo.Open(password);
                Console.WriteLine("Opened repository");
            }
            else
            {
                repo.Initialize(password);
                Console.WriteLine("Initialized repository");
            }

            Console.WriteLine("1. Upload directory (non recursive)");
            Console.WriteLine("2. List all directories");
            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
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
                        new MutableFilesystem(repo, new HmacKey(Convert.FromHexString("1111111111111111111111111111111111111111111111111111111111111111"))).UploadDirectoryNonRecursive(localPath, new DirectoryPath(remotePath));
                    }
                    break;

                case "2":
                    new MutableFilesystem(repo, new HmacKey(Convert.FromHexString("1111111111111111111111111111111111111111111111111111111111111111"))).ListDirectory(new DirectoryPath("/"));
                    break;

                default:
                    break;

            }
        }
    }
}