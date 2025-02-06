using System;

using Wibblr.Grufs.Core;
using Wibblr.Grufs.Storage;
using Wibblr.Grufs.Storage.Server;
using Wibblr.Grufs.Storage.Sftp;
using Wibblr.Grufs.Storage.Sqlite;

namespace Wibblr.Grufs.Cli
{
    public class RepositorySerializer
    {
        public static readonly string RepoName = "repoName";
        public static readonly string EncryptionPassword = "encryptionPassword";
        public static readonly string Protocol = "protocol";
        public static readonly string Host = "host";
        public static readonly string Port = "port";
        public static readonly string Username = "username";
        public static readonly string Password = "password";
        public static readonly string PrivateKeyFile = "";
        public static readonly string BaseDir = "baseDir";

        public string Serialize(Repository repo)
        {
            var items = new List<KeyValuePair<string, object>>();

            void Add(string key, object value)
            {
                items.Add(new KeyValuePair<string, object>(key, value));
            }
      
            Add(RepoName, repo.Name);
            Add(EncryptionPassword, repo.EncryptionPassword);

            if (repo.ChunkStorage is SqliteStorage sqliteStorage)
            {
                Add(Protocol, "sqlite");
                Add(BaseDir, sqliteStorage.BaseDir);
            }
            else if (repo.ChunkStorage is SftpStorage sftpStorage)
            {
                Add(Protocol, "sftp");
                Add(Host, sftpStorage.Credentials.Host ?? "");
                Add(Port, sftpStorage.Credentials.Port);
                Add(Username, sftpStorage.Credentials.Username ?? "");
                Add(Password, sftpStorage.Credentials.Password ?? "");
                Add(PrivateKeyFile, sftpStorage.Credentials.PrivateKey ?? "");
                Add(BaseDir, sftpStorage.BaseDir);
            }
            else if (repo.ChunkStorage is LocalStorage directoryStorage)
            {
                Add(Protocol, "directory");
                Add(BaseDir, directoryStorage.BaseDir);
            }
            else if (repo.ChunkStorage is ServerStorage serverStorage)
            {
                Add(Protocol, "server"); 
                Add(Host, serverStorage.Host);
                Add(Port, serverStorage.Port);
                Add(BaseDir, serverStorage.BaseDir);
            }
            else
            {
                throw new Exception("Unknown storage type");
            }

            return new SkvSerializer().Serialize(items);
        }

        public Repository Deserialize(string skv)
        {
            var items = new SkvSerializer().Deserialize(skv);

            string GetString(string key) => items.Single(x => x.Key == key).Value as string ?? throw new Exception($"Unable to find string {key}");
            int GetInt(string key) => items.Single(x => x.Key == key).Value as int? ?? throw new Exception();

            Console.WriteLine($"Name={GetString(RepoName)}");
            Console.WriteLine($"Protocol={GetString(Protocol)}");

            IChunkStorage storage = GetString(Protocol) switch
            {
                "sqlite" => new SqliteStorage(Path.Join(GetString(BaseDir), GetString(RepoName) + ".sqlite")),

                "sftp" => new SftpStorage(new SftpCredentials
                    {
                        Host = GetString(Host),
                        Port = GetInt(Port),
                        Username = GetString(Username),
                        Password = GetString(Password),
                        PrivateKey = ""
                    },
                    GetString(BaseDir)),

                "directory" => new LocalStorage(GetString(BaseDir)),

                "server" => new ServerStorage(GetString(Host), GetInt(Port), GetString(BaseDir)),

                _ => throw new Exception()
            };

            return new Repository(GetString(RepoName), storage, GetString(EncryptionPassword));
        }
    }
}
