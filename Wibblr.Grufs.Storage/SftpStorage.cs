using System;

using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace Wibblr.Grufs
{
    public class SftpStorage : AbstractFileStorage, IDisposable
    {
        private string _host, _username, _password;
        private int _port;
        private SftpClient _client;

        public SftpStorage(string host, int port, string username, string password, string baseDir)
            : base(baseDir, '/')
        {
            _host = host;
            _port = port;
            _username = username;
            _password = password;

            _client = new SftpClient(_host, _port, _username, _password);
        }

        private bool IsConnected() => _client.IsConnected;

        public SftpStorage EnsureConnected(int maxTries = 10)
        {
            var triesRemaining = maxTries;

            while (!_client.IsConnected)
            {
                try
                {
                    _client.Connect();
                }
                catch (Exception)
                {
                    Thread.Sleep(1000);
                    Console.WriteLine($"Error connecting to host {_host}:{_port}; {--triesRemaining} try(s) remaining");
                    if (triesRemaining <= 0)
                    {
                        throw new Exception();
                    }
                }
            }
            return this;
        }

        override public ReadFileResult ReadFile(string relativePath, out byte[] bytes)
        {
            var path = new StoragePath(_baseDir, _directorySeparator).Concat(relativePath).ToString();
            EnsureConnected();

            try
            {
                bytes = _client.ReadAllBytes(path);
                return ReadFileResult.Success;
            }
            catch (SftpPathNotFoundException)
            {
                bytes = new byte[0];
                return ReadFileResult.PathNotFound;
            }
            catch (SshConnectionException)
            {
                bytes = new byte[0];
                return ReadFileResult.ConnectionError;
            }
            catch (Exception)
            {
                bytes = new byte[0];
                return ReadFileResult.UnknownError;
            }
        }

        override public WriteFileResult WriteFile(string relativePath, byte[] content, OverwriteStrategy overwrite)
        {
            var path = new StoragePath(_baseDir, _directorySeparator).Concat(relativePath).ToString();
            EnsureConnected();

            if (_client.Exists(path))
            {
                switch (overwrite)
                {
                    case OverwriteStrategy.Allow:
                        // fall through
                        break;

                    case OverwriteStrategy.DenyWithError:
                        return WriteFileResult.AlreadyExistsError;

                    case OverwriteStrategy.DenyWithSuccess:
                        return WriteFileResult.Success;
                }
            }
            
            // try to write file
            try
            {
                using (var stream = _client.OpenWrite(path.ToString()))
                {
                    stream.Write(content, 0, content.Length);
                    Console.WriteLine($"Wrote {path}");
                    return WriteFileResult.Success;
                }
            }
            catch (Exception e)
            {
                if (e.Message == "No such file")
                {
                    return WriteFileResult.PathNotFound;
                }

                Console.WriteLine(e.Message);
                return WriteFileResult.UnknownError;
            }
        }

        override public CreateDirectoryResult CreateDirectory(string relativePath)
        {
            var path = new StoragePath(Path.Join(_baseDir, relativePath), _directorySeparator).ToString();
            EnsureConnected();

            try
            {
                _client.CreateDirectory(path);
                Console.WriteLine($"Created directory {relativePath}");
                return CreateDirectoryResult.Success;
            }
            catch (SshConnectionException sce)
            {
                Console.WriteLine(sce.Message);
                return CreateDirectoryResult.ConnectionError;
            }
            catch (SftpPermissionDeniedException spde)
            {
                Console.WriteLine(spde.Message);
                return CreateDirectoryResult.PermissionError;
            }
            catch (SshException se)
            {
                Console.WriteLine(se.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return CreateDirectoryResult.UnknownError;
            }

            try
            {
                var info = _client.Get(path);

                if (info.IsDirectory)
                {
                    return CreateDirectoryResult.AlreadyExists;
                }
                else
                {
                    return CreateDirectoryResult.NonDirectoryAlreadyExists;
                }
            }
            catch (SftpPathNotFoundException spnfe)
            {
                Console.WriteLine(spnfe.Message);
                return CreateDirectoryResult.PathNotFound;
            }
            catch (SshConnectionException sce)
            {
                Console.WriteLine(sce.Message);
                return CreateDirectoryResult.ConnectionError;
            }
        }


        override public bool Exists(string relativePath)
        {
            var path = new StoragePath(Path.Join(_baseDir, relativePath), _directorySeparator).ToString();
            EnsureConnected();

            try
            {
                return _client.Exists(path);
            }
            catch (Exception e)
            {
                return false;
            }
        }

        override public void DeleteDirectory(string relativePath)
        {
            var path = Path.Join(_baseDir, relativePath);
            EnsureConnected();

            try
            {
                DeleteDirectory(_client.Get(path));
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

        override public (List<string> files, List<string> directories) ListDirectoryEntries(string relativePath)
        {
            var path = new StoragePath(Path.Join(_baseDir, relativePath), _directorySeparator).ToString();
            EnsureConnected();

            var files = new List<string>();
            var directories = new List<string>();

            foreach (var item in _client.ListDirectory(path.ToString()))
            {
                if (item.Name == "." || item.Name == "..")
                {
                    continue;
                }
                else if (item.IsRegularFile)
                {
                    files.Add(item.Name);
                }
                else if (item.IsDirectory)
                {
                    directories.Add(item.Name);
                }
            }

            files.Sort();
            directories.Sort();

            return (files, directories);
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
