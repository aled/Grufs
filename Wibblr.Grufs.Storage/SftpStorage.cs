using System;
using System.IO;

using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

using Wibblr.Grufs.Logging;

namespace Wibblr.Grufs.Storage
{
    public class SftpStorage : AbstractFileStorage, IDisposable
    {
        public string Host { get; init; }
        public string Username { get; init; }
        public string Password { get; init; }
        public int Port { get; init; }

        private SftpClient _client;

        public SftpStorage(string host, int port, string username, string password, string baseDir)
            : base(baseDir, '/')
        {
            Host = host;
            Port = port;
            Username = username;
            Password = password;

            _client = new SftpClient(Host, Port, Username, Password);
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
                    Log.WriteLine(0, $"Error connecting to host {Host}:{Port}; {--triesRemaining} try(s) remaining");
                    if (triesRemaining <= 0)
                    {
                        throw new Exception();
                    }
                }
            }
            return this;
        }

        public override void Init()
        {
            EnsureConnected();

            Stack<StoragePath> paths = new Stack<StoragePath>();
            paths.Push(new StoragePath(BaseDir, _directorySeparator));
            while (paths.Any())
            {
                var path = paths.Pop();

                try
                {
                    _client.CreateDirectory("/" + path.ToString());

                    if (!paths.Any())
                    {
                        return;
                    }
                }
                catch (SftpPathNotFoundException)
                {
                    if (path.IsRoot)
                    {
                        throw new Exception("Error creating basedir");
                    }
                    paths.Push(path);
                    paths.Push(path.Parent);
                }
                catch (SshException se)
                {
                    // Will get this if already exists
                }
                catch (Exception e)
                {
                    Log.WriteLine(0, e.Message);
                }
            }

            var info = _client.Get(BaseDir);

            if (!info.IsDirectory)
            {
                throw new Exception("Error creating basedir");
            }
        }

        public override ReadFileStatus ReadFile(string relativePath, out byte[] bytes)
        {
            var path = new StoragePath(BaseDir, _directorySeparator).Concat(relativePath).ToString();
            EnsureConnected();

            try
            {
                bytes = _client.ReadAllBytes(path);
                return ReadFileStatus.Success;
            }
            catch (SftpPathNotFoundException)
            {
                bytes = new byte[0];
                return ReadFileStatus.PathNotFound;
            }
            catch (SshConnectionException)
            {
                bytes = new byte[0];
                return ReadFileStatus.ConnectionError;
            }
            catch (Exception)
            {
                bytes = new byte[0];
                return ReadFileStatus.UnknownError;
            }
        }

        public override WriteFileStatus WriteFile(string relativePath, byte[] content, OverwriteStrategy overwrite)
        {
            var path = new StoragePath(BaseDir, _directorySeparator).Concat(relativePath).ToString();
            EnsureConnected();

            if (_client.Exists(path))
            {
                switch (overwrite)
                {
                    case OverwriteStrategy.Allow:
                        // fall through
                        break;

                    case OverwriteStrategy.Deny:
                        return WriteFileStatus.OverwriteDenied;
                }
            }
            
            // try to write file
            try
            {
                using (var stream = _client.OpenWrite(path.ToString()))
                {
                    stream.Write(content, 0, content.Length);
                    //Log.WriteLine(0, $"Wrote {path}");
                    return WriteFileStatus.Success;
                }
            }
            catch (Exception e)
            {
                if (e.Message == "No such file")
                {
                    return WriteFileStatus.PathNotFound;
                }

                //Log.WriteLine(0, e.Message);
                return WriteFileStatus.Error;
            }
        }

        public override CreateDirectoryStatus CreateDirectory(string relativePath)
        {
            var path = new StoragePath(Path.Join(BaseDir, relativePath), _directorySeparator).ToString();
           
            EnsureConnected();

            try
            {
                _client.CreateDirectory(path.ToString());
                return CreateDirectoryStatus.Success;
            }
            catch (SftpPathNotFoundException)
            {
                return CreateDirectoryStatus.PathNotFound;
            }
            catch (SshConnectionException sce)
            {
                Log.WriteLine(0, sce.Message);
                return CreateDirectoryStatus.ConnectionError;
            }
            catch (SftpPermissionDeniedException spde)
            {
                Log.WriteLine(0, spde.Message);
                return CreateDirectoryStatus.PermissionError;
            }
            catch (SshException se)
            {
                Log.WriteLine(1, se.Message);
            }
            catch (Exception e)
            {
                Log.WriteLine(0, e.Message);
                return CreateDirectoryStatus.UnknownError;
            }

            try
            {
                var info = _client.Get(path);

                if (info.IsDirectory)
                {
                    return CreateDirectoryStatus.AlreadyExists;
                }
                else
                {
                    return CreateDirectoryStatus.NonDirectoryAlreadyExists;
                }
            }
            catch (SftpPathNotFoundException spnfe)
            {
                Log.WriteLine(1, spnfe.Message);
                return CreateDirectoryStatus.PathNotFound;
            }
            catch (SshConnectionException sce)
            {
                Log.WriteLine(0, sce.Message);
                return CreateDirectoryStatus.ConnectionError;
            }
        }


        public override bool Exists(string relativePath)
        {
            var path = new StoragePath(Path.Join(BaseDir, relativePath), _directorySeparator).ToString();
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

        public override void DeleteDirectory(string relativePath)
        {
            var path = Path.Join(BaseDir, relativePath);
            EnsureConnected();

            try
            {
                DeleteDirectory(_client.Get(path));
            }
            catch(Exception ex)
            {
                Log.WriteLine(0, ex.Message);
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

        public override (List<string> files, List<string> directories) ListDirectoryEntries(string relativePath)
        {
            var path = new StoragePath(Path.Join(BaseDir, relativePath), _directorySeparator).ToString();
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
