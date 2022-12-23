using System;

using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace Wibblr.Grufs
{
    public class SftpStorage : IFileStorage, IDisposable
    {
        private string _host, _username, _password, _baseDir;
        private SftpClient _client;

        public SftpStorage(string host, string username, string password)
        {
            _host = host;
            _username = username;
            _password = password;

            _client = new SftpClient(_host, _username, _password);
        }

        public IFileStorage WithBaseDir(string baseDir)
        {
            _baseDir = baseDir;
            return this;
        }

        public void EnsureConnected()
        {
            if (!_client.IsConnected)
            {
                _client.Connect();
            }
        }

        public bool TryDownload(string path, out byte[] bytes)
        {
            var fullRelativePath = Path.Combine(_baseDir, path).Replace("\\", "/"); ;
            EnsureConnected();
            try
            {
                bytes = _client.ReadAllBytes(fullRelativePath);
                return true;
            }
            catch (SftpPathNotFoundException)
            {
                bytes = new byte[0];
                return false;
            }
            catch (Exception)
            {
                bytes = new byte[0];
                throw;
            }
        }

        public bool Upload(string path, byte[] content, OverwriteStrategy overwrite)
        {
            // treat path as relative to the base path, even if it starts with a separator
            if (path.StartsWith("/"))
            {
                path = path.Substring(1);
            }
            var fullRelativePath = Path.Combine(_baseDir, path).Replace("\\", "/");
            EnsureConnected();
            
            if (_client.Exists(fullRelativePath))
            {
                switch (overwrite)
                {
                    case OverwriteStrategy.Allow:
                        _client.Delete(fullRelativePath);
                        break;

                    case OverwriteStrategy.DenyWithError:
                        return false;

                    case OverwriteStrategy.DenyWithSuccess: 
                        return true;

                    case OverwriteStrategy.VerifyChecksum:
                        throw new NotImplementedException();
               }
            }

            foreach (var directory in ((IFileStorage)this).GetParentDirectories(fullRelativePath))
            {
                if (!_client.Exists(directory))
                {
                    _client.CreateDirectory(directory);
                }
            }
            using (var stream = _client.OpenWrite(fullRelativePath))
            {
                stream.Write(content, 0, content.Length);
                return true;
            }
        }

        public bool Exists(string path = "")
        {
            // treat path as relative to the base path, even if it starts with a separator
            if (path.StartsWith("/"))
            {
                path = path.Substring(1);
            }
            var fullRelativePath = Path.Combine(_baseDir, path).Replace("\\", "/");

            EnsureConnected();

            return _client.Exists(fullRelativePath);
        }

        public void DeleteDirectory(string path)
        {
            // treat path as relative to the base path, even if it starts with a separator
            if (path.StartsWith("/"))
            {
                path = path.Substring(1);
            }
            var fullRelativePath = Path.Combine(_baseDir, path).Replace("\\", "/");
            EnsureConnected();

            try
            {
                DeleteDirectory(_client.Get(fullRelativePath));
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            void DeleteDirectory(SftpFile directory)
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

        public IEnumerable<string> ListFiles(string path)
        {
            int recursionCounter = 0;

            // treat path as relative to the base path, even if it starts with a separator
            if (path.StartsWith("/"))
            {
                path = path.Substring(1);
            }
            var fullRelativePath = Path.Combine(_baseDir, path).Replace("\\", "/");
            var fullPath = _client.Get(fullRelativePath).FullName;

            EnsureConnected();

            return List(fullPath, fullPath);

            IEnumerable<string> List(string fullPath, string originalFullPath)
            {
                recursionCounter++;

                if (recursionCounter == 10)
                    throw new Exception();

                var items = _client.ListDirectory(fullPath)
                    .OrderBy(x => x.Name)
                    .Where(x => x.Name != "." && x.Name != "..")
                    .ToList();

                foreach (var item in items.Where(x => x.IsRegularFile))
                {
                    yield return Path.GetRelativePath(originalFullPath, item.FullName);
                }

                foreach (var item in items.Where(x => x.IsDirectory))
                {
                    foreach (var x in List(item.FullName, originalFullPath))
                    {
                        {
                            yield return x.Replace("\\", "/");
                        }
                    }
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
