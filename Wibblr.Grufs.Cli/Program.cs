using System.Reflection.Metadata.Ecma335;
using System.Text.Json;

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
            var password = Console.ReadLine();

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

            var localBaseDir = "c:\\temp\\grufs-testing";

            Console.WriteLine("1. Upload directory (non recursive)");
            var choice = Console.ReadKey().KeyChar;

            switch (choice)
            {
                case '1':
                    {
                        Console.WriteLine($"Enter local file path (relative to {localBaseDir})");
                        var relativePath = Console.ReadLine();
                        var fullLocalPath = Path.Combine(localBaseDir, relativePath);

                        if (!Directory.Exists(fullLocalPath))
                        {
                            Console.WriteLine($"No such directory: {fullLocalPath}");
                        }
                        else
                        {
                            var remotePath = relativePath;
                            Console.WriteLine($"Uploading {fullLocalPath} to {remotePath}");
                            repo.UploadDirectoryNonRecursive(fullLocalPath, new RepositoryDirectoryPath(remotePath));
                        }
                    }
                    break;
                default:
                    break;

            }
        }
    }
}