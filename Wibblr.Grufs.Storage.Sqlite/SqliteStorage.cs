using Microsoft.Data.Sqlite;

namespace Wibblr.Grufs.Storage.Sqlite
{
    public sealed class SqliteStorage : IChunkStorage, IDisposable
    {
        public string BaseDir { get; init; }

        private SqliteConnection _connection;

        public SqliteStorage(string path) 
        {
            var fileInfo = new FileInfo(path);
            var directory = fileInfo.Directory;
            if (directory == null)
            {
                throw new Exception("No directory found");
            }

            if (!directory.Exists)
            {
                directory.Create();
            }

            BaseDir = directory.FullName;
            _connection = new SqliteConnection($"Data Source={path}");

            // create database and table if required
            _connection.Open();

            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA page_size = 512; CREATE TABLE IF NOT EXISTS Chunk (Address BLOB NOT NULL PRIMARY KEY, Content BLOB NOT NULL) WITHOUT ROWID;";
                cmd.ExecuteNonQuery();
            }
        }

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
            SqliteConnection.ClearAllPools(); // this is needed otherwise the database file will fail to delete
        }

        public bool Exists(Address address)
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM Chunk WHERE Address = $P1);";
                cmd.Parameters.AddWithValue("$P1", address.ToSpan().ToArray());
                return Convert.ToBoolean(cmd.ExecuteScalar());
            }
        }

        public IEnumerable<Address> ListAddresses()
        {
            throw new NotImplementedException();
        }

        public PutStatus Put(EncryptedChunk chunk, OverwriteStrategy overwriteStrategy)
        {
            try
            {
                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = overwriteStrategy switch
                    {
                        OverwriteStrategy.Deny => "INSERT OR IGNORE INTO Chunk (Address, Content) VALUES ($P1, $P2);",
                        OverwriteStrategy.Allow => "INSERT INTO Chunk (Address, Content) VALUES ($P1, $P2) ON CONFLICT(Address) DO UPDATE SET Content = Excluded.Content;",
                        _ => throw new Exception()
                    };
                    cmd.Parameters.AddWithValue("$P1", chunk.Address.ToSpan().ToArray());
                    cmd.Parameters.AddWithValue("$P2", chunk.Content);

                    int rowsInserted = cmd.ExecuteNonQuery();

                    return rowsInserted switch
                    {
                        0 => PutStatus.OverwriteDenied,
                        1 => PutStatus.Success,
                        _ => PutStatus.Unknown
                    };
                }
            }
            catch (Exception ex)
            {
                return PutStatus.Error;
            }
        }

        public bool TryGet(Address address, out EncryptedChunk chunk)
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT Content FROM Chunk WHERE Address = $P1;";
                cmd.Parameters.AddWithValue("$P1", address.ToSpan().ToArray());
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var content = ((MemoryStream)reader.GetStream(reader.GetOrdinal("Content"))).ToArray();
                        chunk = new EncryptedChunk(address, content);
                        return true;
                    }
                }
            }
            chunk = default;
            return false;
        }
    }
}