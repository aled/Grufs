using System;

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
        private SqliteCommand _beginTransactionCommand;
        private SqliteCommand _commitTransactionCommand;

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

            _beginTransactionCommand = _connection.CreateCommand();
            _beginTransactionCommand.CommandText = "BEGIN TRANSACTION;";

            _commitTransactionCommand = _connection.CreateCommand();
            _commitTransactionCommand.CommandText = "COMMIT;";
        }

        public async Task InitAsync(CancellationToken token)
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "CREATE TABLE IF NOT EXISTS Chunk (Address BLOB NOT NULL PRIMARY KEY, Content BLOB NOT NULL) WITHOUT ROWID;";
                await cmd.ExecuteNonQueryAsync(token);
            }
        }

        public async Task<long> CountAsync(CancellationToken token)
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT Count(*) FROM Chunk;";
                return Convert.ToInt64(await cmd.ExecuteScalarAsync(token));
            }
        }

        public async Task<bool> ExistsAsync(Address address, CancellationToken token)
        {
            var cmd = _existsCommand;
            cmd.Parameters[0].Value = address.ToSpan().ToArray();
            return Convert.ToBoolean(await cmd.ExecuteScalarAsync());
        }

        public IAsyncEnumerable<Address> ListAddressesAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        int rowsInTransaction = 0;

        public async Task<PutStatus> PutAsync(EncryptedChunk chunk, OverwriteStrategy overwriteStrategy, CancellationToken token)
        {
            if (rowsInTransaction == 100)
            {
                Flush();
            }

            if (rowsInTransaction == 0)
            {
                await _beginTransactionCommand.ExecuteNonQueryAsync(token);
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
            catch
            {
                return PutStatus.Error;
            }
        }

        public async Task<EncryptedChunk?> GetAsync(Address address, CancellationToken token)
        {
            Flush();

            var cmd = _selectCommand;
            cmd.Parameters[0].Value = address.ToSpan().ToArray();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (reader.Read())
                {
                    var content = ((MemoryStream)reader.GetStream(reader.GetOrdinal("Content"))).ToArray();
                    return new EncryptedChunk(address, content);
                }
            }
            return null;
        }

        private async Task FlushAsync()
        {
            if (rowsInTransaction > 0)
            {
                await _commitTransactionCommand.ExecuteNonQueryAsync();
                rowsInTransaction = 0;
            }
        }

        public void Flush()
        {
            // TODO: Make async. Need to implement IAsyncDisposable?
            if (rowsInTransaction > 0)
            {
                _commitTransactionCommand.ExecuteNonQuery();
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
