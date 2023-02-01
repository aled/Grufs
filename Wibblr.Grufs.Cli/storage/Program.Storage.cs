using System.Security.Cryptography;
using System.Text;

using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Cli
{
    partial class Program
    {
        static void Storage(string[] args)
        {
            if (args.Length == 0) 
            {
                throw new UsageException();
            }
            if (args[0] == "init")
            {
                Init(args.Skip(1).ToArray());
            }
        }

        static void Init(string[] args) 
        {
            var options = ParseArgs(args, "name", "protocol", "host", "port", "user", "storage-password", "basedir", "encryption-password");

            var name = ExtractOrRead(options, "name", "Enter user-friendly name for repository: ", "[default]");

            // check if repository is already registered
            if (File.Exists(Path.Join(LocalConfigDirectory, "storage", name)))
            {
                Console.WriteLine($"Repository '{name}' already registered");
                return;
            }

            var protocol = ExtractOrRead(options, "protocol", "Enter protocol", "directory");
            var baseDir = ExtractOrRead(options, "baseDir", "Enter SFTP directory", "/tank7/grufs/repo2");
            var encryptionPassword = ExtractOrRead(options, "encryption-password", "Enter encryption password", Convert.ToHexString(RandomNumberGenerator.GetBytes(64)));

            switch (protocol)
            {
                case "sftp":
                    {
                        var host = ExtractOrRead(options, "host", "Enter hostname", "localhost");
                        var port = Convert.ToInt32(ExtractOrRead(options, "port", "Enter port", "22"));
                        var user = ExtractOrRead(options, "user", "Enter SFTP username", "grufs");
                        var storagePassword = ExtractOrRead(options, "storagePassword", "Enter SFTP password", "");

                        SftpStorage storage = new SftpStorage(host, port, user, storagePassword, baseDir);

                        try
                        {
                            storage.EnsureConnected();
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("Unable to connect to repository");
                            return;
                        }

                        var repo = new Repository(storage);

                        if (repo.Initialize(encryptionPassword))
                        {
                            // write to local config
                            File.WriteAllText(
                                Path.Join(LocalConfigDirectory, "storage", name),
                                $"name:{name}\n" +
                                $"protocol:{protocol}\n" +
                                $"host:{host}\n" +
                                $"port:{port}\n" +
                                $"user:{user}\n" +
                                $"storagePassword:{Convert.ToHexString(Encoding.UTF8.GetBytes(storagePassword))}\n" +
                                $"baseDir:{baseDir}\n" +
                                $"encryptionPassword:{encryptionPassword}\n");

                            Console.WriteLine("Storage initialized");
                        }
                    }
                    break;

                case "directory":
                    {
                        DirectoryStorage storage = new DirectoryStorage(baseDir);
                        var repo = new Repository(storage);

                        if (repo.Initialize(encryptionPassword))
                        {
                            // write to local config
                            File.WriteAllText(
                                Path.Join(LocalConfigDirectory, "storage", name),
                                $"name:{name}\n" +
                                $"protocol:{protocol}\n" +
                                $"baseDir:{baseDir}\n" +
                                $"encryptionPassword:{encryptionPassword}\n");

                            Console.WriteLine("Storage initialized");
                        }
                    }
                    break;


                default:
                    Console.WriteLine("Storage type must be 'sftp' or 'directory'");
                    break;
            }
        }
    }
}