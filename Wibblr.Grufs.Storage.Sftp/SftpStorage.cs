using static Wibblr.Grufs.Storage.FileStorageUtils;
using Wibblr.Grufs.Logging;

using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;
using System.Text;

namespace Wibblr.Grufs.Storage.Sftp
{
    public class SftpStorage : IChunkStorage, IDisposable
    {
        public string BaseDir { get; init; }
        public SftpCredentials Credentials { get; init; }

        private SftpClient _client;

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

        public SftpStorage EnsureConnected(int maxTries = 3)
        {
            var triesRemaining = maxTries;

            while (!_client.IsConnected)
            {
                try
                {
                    _client.Connect();
                }
                catch (SshAuthenticationException sae)
                {
                    Log.WriteLine(0, $"Authentication error connecting to host {Credentials.Host}:{Credentials.Port}; {sae.Message}");
                    throw;
                }
                catch (Exception e)
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

        public void Init()
        {
            CreateDirectory(BaseDir);

            var info = _client.Get(BaseDir);

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
        private bool CreateDirectory(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);

            EnsureConnected();

            try
            {
                _client.CreateDirectory(path);
                return true;
            }
            catch (SshConnectionException sce)
            {
                Log.WriteLine(0, sce.Message);
                return false;
            }
            catch (SftpPermissionDeniedException spde)
            {
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
                var info = _client.Get(path);

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
                if (CreateDirectory(parent))
                {
                    _client.CreateDirectory(path);
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
        private void WriteAllBytes(string path, byte[] content)
        {
            try
            {
                using (var stream = _client.OpenWrite(path.ToString()))
                {
                    stream.Write(content, 0, content.Length);
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
        public bool Exists(string path)
        {
            EnsureConnected();

            try
            {
                return _client.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void CreateParentsAndWriteAllBytes(string path, byte[] content)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);

            try
            {
                WriteAllBytes(path, content);
            }
            catch (DirectoryNotFoundException)
            {
                var parent = GetDirectoryName(path);

                if (parent != null)
                {
                    CreateDirectory(parent);
                    WriteAllBytes(path, content);
                }
            }
        }

        public PutStatus Put(EncryptedChunk chunk, OverwriteStrategy overwriteStrategy)
        {
            var path = ToUnixPath(Path.Join(BaseDir, GeneratePath(chunk.Address)));

            EnsureConnected();

            switch (overwriteStrategy)
            {
                case OverwriteStrategy.Allow:
                    CreateParentsAndWriteAllBytes(path, chunk.Content);
                    return PutStatus.Success;

                case OverwriteStrategy.Deny:
                    if (Exists(path))
                    {
                        return PutStatus.OverwriteDenied;
                    }

                    CreateParentsAndWriteAllBytes(path, chunk.Content);
                    return PutStatus.Success;

                default:
                    throw new Exception("Invalid overwrite strategy");
            }
        }

        public bool TryGet(Address address, out EncryptedChunk chunk)
        {
            var path = ToUnixPath(Path.Join(BaseDir, GeneratePath(address)));

            EnsureConnected();

            try
            {
                chunk = new EncryptedChunk(address, _client.ReadAllBytes(path));
                return true;
            }
            catch (SftpPathNotFoundException)
            {
                chunk = default;
                return false;
            }
            catch (SshConnectionException)
            {
                chunk = default;
                return false;
            }
            catch (Exception)
            {
                chunk = default;
                return false;
            }
        }

        private IEnumerable<string> ListChunkFiles()
        {
            foreach (var grandparent in _client.ListDirectory(BaseDir))
            {
                if (grandparent.IsDirectory && grandparent.Name.Length == 2 && IsHexString(grandparent.Name))
                {
                    foreach (var parent in _client.ListDirectory(grandparent.FullName))
                    {
                        if (parent.IsDirectory && parent.Name.Length == 2 && IsHexString(parent.Name))
                        {
                            foreach (var address in _client.ListDirectory(parent.FullName))
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
       
        public long Count()
        {
            return ListChunkFiles().Count();
        }

        public bool Exists(Address address)
        {
            return Exists(ToUnixPath(Path.Join(BaseDir, GeneratePath(address))));
        }

        public IEnumerable<Address> ListAddresses()
        {
            foreach (var file in ListChunkFiles())
            {
                yield return new Address(Convert.FromHexString(file));
            }
        }

        public void Flush()
        {
            // no op
        }

        public void DeleteDirectory(string relativePath)
        {
            var path = ToUnixPath(Path.Join(BaseDir, relativePath));
            EnsureConnected();

            try
            {
                DeleteDirectory(_client.Get(path));
            }
            catch (Exception ex)
            {
                Log.WriteLine(0, ex.Message);
            }

            void DeleteDirectory(ISftpFile directory)
            {
                if (directory.IsDirectory)
                {
                    foreach (var entry in _client.ListDirectory(directory.FullName))
                    {
                        if ((entry.Name != ".") && (entry.Name != ".."))
                        {
                            if (entry.IsDirectory)
                            {
                                DeleteDirectory(entry);
                            }
                            else
                            {
                                _client.DeleteFile(entry.FullName);
                            }
                        }
                    }
                    _client.DeleteDirectory(directory.FullName);
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
