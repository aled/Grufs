using static Wibblr.Grufs.Storage.FileStorageUtils;
using Wibblr.Grufs.Logging;

using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;
using System.Text;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;

namespace Wibblr.Grufs.Storage.Sftp
{
    public class SftpStorage : IChunkStorage, IDisposable
    {
        public string BaseDir { get; init; }

        public SftpCredentials Credentials { get; init; }

        private SftpClient _client;

        private int _connectionRetryCount = 3;

        public SftpStorage(SftpCredentials credentials, string baseDir)
        {
            Credentials = credentials;
            var connectionInfo = new ConnectionInfo(credentials.Host, credentials.Port, credentials.Username, GetAuthenticationMethods(credentials).ToArray());

            _client = new SftpClient(connectionInfo);
            BaseDir = ToUnixPath(baseDir);
        }

        private IEnumerable<AuthenticationMethod> GetAuthenticationMethods(SftpCredentials credentials)
        {
            if (!string.IsNullOrEmpty(credentials.PrivateKey))
            {
                var stream = new MemoryStream(Encoding.ASCII.GetBytes(credentials.PrivateKey));
                yield return new PrivateKeyAuthenticationMethod(credentials.Username, new PrivateKeyFile(stream));
            }

            if (!string.IsNullOrEmpty(credentials.Password))
            {
                yield return new PasswordAuthenticationMethod(credentials.Username, credentials.Password);
            }
        }

        private string ToUnixPath(string path)
        {
            return Path.TrimEndingDirectorySeparator(path.Replace("\\", "/"));
        }

        /// <summary>
        /// This has the same signature as System.IO.Directory.GetDirectoryName
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string? GetDirectoryName(string path)
        {
            var lastIndex = path.LastIndexOf('/');

            if (lastIndex > 0)
            {
                return path.Substring(0, path.LastIndexOf("/"));
            }

            return null;
        }

        private bool IsConnected() => _client.IsConnected;

        public async Task<SftpStorage> EnsureConnectedAsync(CancellationToken token)
        {
            var triesRemaining = _connectionRetryCount;

            while (!_client.IsConnected)
            {
                try
                {
                    await _client.ConnectAsync(token);
                }
                catch (SshAuthenticationException sae)
                {
                    Log.WriteLine(0, $"Authentication error connecting to host {Credentials.Host}:{Credentials.Port}; {sae.Message}");
                    throw;
                }
                catch
                {
                    Thread.Sleep(500);
                    Log.WriteLine(0, $"Error connecting to host {Credentials.Host}:{Credentials.Port}; {--triesRemaining} try(s) remaining");
                    if (triesRemaining <= 0)
                    {
                        throw new Exception();
                    }
                }
            }
            return this;
        }

        public async Task InitAsync(CancellationToken token)
        {
            await CreateDirectoryAsync(BaseDir, token);

            var info = await _client.GetAsync(BaseDir, token);

            if (!info.IsDirectory)
            {
                throw new Exception("Error creating basedir");
            }
        }

        /// <summary>
        /// This has the same signature as System.IO.Directory.CreateDirectory
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private async Task<bool> CreateDirectoryAsync(string path, CancellationToken token)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);

            await EnsureConnectedAsync(token);

            try
            {
                await _client.CreateDirectoryAsync(path);
                return true;
            }
            catch (SshConnectionException sce)
            {
                Log.WriteLine(0, sce.Message);
                return false;
            }
            catch (SftpPermissionDeniedException spde)
            {
                Log.WriteLine(0, spde.Message);
                return false;
            }
            catch (Exception e)
            {
                if (e is not SftpPathNotFoundException && e is not SshException)
                {
                    Log.WriteLine(0, e.Message);
                    return false;
                }
            }

            // either directory already existed, or a parent did not exist, or a file with the same name exists
            try
            {
                var info = await _client.GetAsync(path, token);

                if (!info.IsDirectory)
                {
                    Log.WriteLine(0, $"Cannot create directory '{path}' - non directory already exists");
                    return false;
                }
            }
            catch (SshConnectionException sce)
            {
                Log.WriteLine(0, sce.Message);
                return false;
            }
            catch (SftpPathNotFoundException)
            {
            }

            // create all parents
            var parent = GetDirectoryName(path);

            if (!string.IsNullOrEmpty(parent))
            {
                if (await CreateDirectoryAsync(parent, token))
                {
                    await _client.CreateDirectoryAsync(path, token);
                    return true;
                }
                return false;
            }

            return false;
        }

        /// <summary>
        /// This uses the same signature as System.IO.File.WriteAllBytes
        /// </summary>
        /// <param name="path"></param>
        /// <param name="content"></param>
        /// <exception cref=""></exception>
        /// <exception cref="Exception"></exception>
        private async Task WriteAllBytesAsync(string path, byte[] content, CancellationToken token)
        {
            try
            {
                using (var stream = _client.OpenWrite(path.ToString()))
                {
                    await stream.WriteAsync(content, 0, content.Length, token);
                    //Log.WriteLine(0, $"Wrote {path}");
                }
            }
            catch (Exception e)
            {
                if (e.Message == "No such file")
                {
                    throw new DirectoryNotFoundException();
                }
                throw;
            }
        }

        /// <summary>
        /// This has the same signature as System.IO.File.Exists
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public async Task<bool> ExistsAsync(string path, CancellationToken token)
        {
            await EnsureConnectedAsync(token);

            try
            {
                return await _client.ExistsAsync(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task CreateParentsAndWriteAllBytesAsync(string path, byte[] content, CancellationToken token)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);

            try
            {
                await WriteAllBytesAsync(path, content, token);
            }
            catch (DirectoryNotFoundException)
            {
                var parent = GetDirectoryName(path);

                if (parent != null)
                {
                    await CreateDirectoryAsync(parent, token);
                    await WriteAllBytesAsync(path, content, token);
                }
            }
        }

        public async Task<PutStatus> PutAsync(EncryptedChunk chunk, OverwriteStrategy overwriteStrategy, CancellationToken token)
        {
            var path = ToUnixPath(Path.Join(BaseDir, GeneratePath(chunk.Address)));

            await EnsureConnectedAsync(token);

            switch (overwriteStrategy)
            {
                case OverwriteStrategy.Allow:
                    await CreateParentsAndWriteAllBytesAsync(path, chunk.Content, token);
                    return PutStatus.Success;

                case OverwriteStrategy.Deny:
                    if (await ExistsAsync(path, token))
                    {
                        return PutStatus.OverwriteDenied;
                    }

                    await CreateParentsAndWriteAllBytesAsync(path, chunk.Content, token);
                    return PutStatus.Success;

                default:
                    throw new Exception("Invalid overwrite strategy");
            }
        }

        public async Task<EncryptedChunk?> GetAsync(Address address, CancellationToken token)
        {
            var path = ToUnixPath(Path.Join(BaseDir, GeneratePath(address)));

            await EnsureConnectedAsync(token);

            try
            {
                return new EncryptedChunk(address, _client.ReadAllBytes(path));
            }
            catch (SftpPathNotFoundException)
            {
                return null;
            }
            catch (SshConnectionException)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async IAsyncEnumerable<string> ListChunkFilesAsync([EnumeratorCancellation] CancellationToken token)
        {
            await foreach (var grandparent in _client.ListDirectoryAsync(BaseDir, token))
            {
                if (grandparent.IsDirectory && grandparent.Name.Length == 2 && IsHexString(grandparent.Name))
                {
                    await foreach (var parent in _client.ListDirectoryAsync(grandparent.FullName, token))
                    {
                        if (parent.IsDirectory && parent.Name.Length == 2 && IsHexString(parent.Name))
                        {
                            await foreach (var address in _client.ListDirectoryAsync(parent.FullName, token))
                            {
                                if (address.IsRegularFile && address.Name.Length == Address.Length * 2 && IsHexString(address.Name))
                                {
                                    yield return address.Name;
                                }
                            }
                        }
                    }
                }
            }
        }
       
        public async Task<long> CountAsync(CancellationToken token)
        {
            return await ListChunkFilesAsync(token).CountAsync();
        }

        public Task<bool> ExistsAsync(Address address, CancellationToken token)
        {
            return ExistsAsync(ToUnixPath(Path.Join(BaseDir, GeneratePath(address))), token);
        }

        public async IAsyncEnumerable<Address> ListAddressesAsync([EnumeratorCancellation] CancellationToken token)
        {
            await foreach (var file in ListChunkFilesAsync(token))
            {
                yield return new Address(Convert.FromHexString(file));
            }
        }

        public void Flush()
        {
            // no op
        }

        public async Task DeleteDirectory(string relativePath, CancellationToken token)
        {
            var path = ToUnixPath(Path.Join(BaseDir, relativePath));
            await EnsureConnectedAsync(token);

            try
            {
                await DeleteDirectoryAsync(_client.Get(path), token);
            }
            catch (Exception ex)
            {
                Log.WriteLine(0, ex.Message);
            }

            async Task DeleteDirectoryAsync(ISftpFile directory, CancellationToken token)
            {
                if (directory.IsDirectory)
                {
                    await foreach (var entry in _client.ListDirectoryAsync(directory.FullName, token))
                    {
                        if ((entry.Name != ".") && (entry.Name != ".."))
                        {
                            if (entry.IsDirectory)
                            {
                                await DeleteDirectoryAsync(entry, token);
                            }
                            else
                            {
                                await _client.DeleteFileAsync(entry.FullName, token);
                            }
                        }
                    }
                    await _client.DeleteDirectoryAsync(directory.FullName, token);
                }
            }
        }

        public void Dispose()
        {
            if (_client != null)
            {
                _client.Dispose();
            }
        }
    }
}
