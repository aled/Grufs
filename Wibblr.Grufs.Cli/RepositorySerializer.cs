﻿using System;

using Wibblr.Grufs.Core;
using Wibblr.Grufs.Storage;
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
                Add(Host, sftpStorage.Host);
                Add(Port, sftpStorage.Port);
                Add(Username, sftpStorage.Username);
                Add(Password, sftpStorage.Password);
                Add(BaseDir, sftpStorage.BaseDir);
            }
            else if (repo.ChunkStorage is DirectoryStorage directoryStorage)
            {
                Add(Protocol, "directory");
                Add(BaseDir, directoryStorage.BaseDir);
            }
            else
            {
                throw new Exception();
            }

            return new SkvSerializer().Serialize(items);
        }

        public Repository Deserialize(string skv)
        {
            var items = new SkvSerializer().Deserialize(skv);

            string GetString(string key) => items.Single(x => x.Key == key).Value as string ?? throw new Exception($"Unable to find string {key}");
            int GetInt(string key) => items.Single(x => x.Key == key).Value as int? ?? throw new Exception();

            IChunkStorage storage = GetString(Protocol) switch
            {
                "sqlite" => new SqliteStorage(Path.Join(GetString(BaseDir), GetString(RepoName) + ".sqlite")),
                "sftp" => new SftpStorage(GetString(Host), GetInt(Port), GetString(Username), GetString(Password), GetString(BaseDir)),
                "directory" => new DirectoryStorage(GetString(BaseDir)),
                _ => throw new Exception()
            };

            return new Repository(GetString(RepoName), storage, GetString(EncryptionPassword));
        }
    }
}
