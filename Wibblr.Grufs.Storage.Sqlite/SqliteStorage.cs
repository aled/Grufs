using System.Net;

using Microsoft.Data.Sqlite;

namespace Wibblr.Grufs.Storage.Sqlite
{
    public sealed class SqliteStorage : IChunkStorage, IDisposable
    {
        public string BaseDir { get; init; }

        private SqliteConnection _connection;
        private SqliteCommand _insertCommand;
        private SqliteCommand _upsertCommand;
        private SqliteCommand _existsCommand;
        private SqliteCommand _selectCommand;

        private List<EncryptedChunk> _chunks = new List<EncryptedChunk>();

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
                cmd.CommandText = "PRAGMA page_size = 512;";
                cmd.ExecuteNonQuery();
            }
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA journal_mode=WAL";
                var result = cmd.ExecuteScalar();
                //Console.WriteLine(result);
            }
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS Chunk (Address BLOB NOT NULL PRIMARY KEY, Content BLOB NOT NULL) WITHOUT ROWID;";
                cmd.ExecuteNonQuery();
            }

            _insertCommand = _connection.CreateCommand();
            _insertCommand.CommandText = "INSERT OR IGNORE INTO Chunk (Address, Content) VALUES ($P1, $P2);";
            _insertCommand.Parameters.Add(new SqliteParameter("$P1", SqliteType.Blob));
            _insertCommand.Parameters.Add(new SqliteParameter("$P2", SqliteType.Blob));

            _upsertCommand = _connection.CreateCommand();
            _upsertCommand.CommandText = "INSERT INTO Chunk (Address, Content) VALUES ($P1, $P2) ON CONFLICT(Address) DO UPDATE SET Content = Excluded.Content;";
            _upsertCommand.Parameters.Add(new SqliteParameter("$P1", SqliteType.Blob));
            _upsertCommand.Parameters.Add(new SqliteParameter("$P2", SqliteType.Blob));

            _existsCommand = _connection.CreateCommand();
            _existsCommand.CommandText = "SELECT EXISTS(SELECT 1 FROM Chunk WHERE Address = $P1);";
            _existsCommand.Parameters.Add(new SqliteParameter("$P1", SqliteType.Blob));

            _selectCommand = _connection.CreateCommand();
            _selectCommand.CommandText = "SELECT Content FROM Chunk WHERE Address = $P1;";
            _selectCommand.Parameters.Add(new SqliteParameter("$P1", SqliteType.Blob));

        public void Init()
        {
        }

        public long Count()
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT Count(*) FROM Chunk;";
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
        }

        public bool Exists(Address address)
        {
            var cmd = _existsCommand;
            cmd.Parameters[0].Value = address.ToSpan().ToArray();
            return Convert.ToBoolean(cmd.ExecuteScalar());
        }

        public IEnumerable<Address> ListAddresses()
        {
            throw new NotImplementedException();
        }

        int rowsInTransaction = 0;

        public PutStatus Put(EncryptedChunk chunk, OverwriteStrategy overwriteStrategy)
        {
            if (rowsInTransaction == 100)
            {
                Flush();
            }

            if (rowsInTransaction == 0)
            {
                using(var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = "BEGIN TRANSACTION;";
                    cmd.ExecuteScalar();
                }
            }

            try
            {
                var cmd = overwriteStrategy switch
                {
                    OverwriteStrategy.Deny => _insertCommand,
                    OverwriteStrategy.Allow => _upsertCommand,
                    _ => throw new Exception()
                };
                cmd.Parameters[0].Value = chunk.Address.ToSpan().ToArray();
                cmd.Parameters[1].Value = chunk.Content;

                int rowsInserted = cmd.ExecuteNonQuery();
                rowsInTransaction++;

                return rowsInserted switch
                {
                    0 => PutStatus.OverwriteDenied,
                    1 => PutStatus.Success,
                    _ => PutStatus.Unknown
                };
            }
            catch (Exception ex)
            {
                return PutStatus.Error;
            }
        }

        public bool TryGet(Address address, out EncryptedChunk chunk)
        {
            Flush();

            var cmd = _selectCommand;
            cmd.Parameters[0].Value = address.ToSpan().ToArray();
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    var content = ((MemoryStream)reader.GetStream(reader.GetOrdinal("Content"))).ToArray();
                    chunk = new EncryptedChunk(address, content);
                    return true;
                }
            }
            chunk = default;
            return false;
        }

        public void Flush()
        {
            if (rowsInTransaction > 0)
            {
                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText = "COMMIT;";
                    cmd.ExecuteScalar();
                }
                rowsInTransaction = 0;
            }
        }

        public void Dispose()
        {
            Flush();

            _insertCommand.Dispose();
            _upsertCommand.Dispose();
            _connection.Close();
            _connection.Dispose();
            SqliteConnection.ClearAllPools(); // this is needed otherwise the database file will fail to delete
        }
    }
}
