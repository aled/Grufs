using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

using Renci.SshNet;

namespace Wibblr.Grufs
{
    public class SftpStorage : IFileStorage, IDisposable
    {
        private string _host, _username, _password, _baseDir;
        private SftpClient _client;

        public SftpStorage(string host, string username, string password, string baseDir)
        {
            _host = host;
            _username = username;
            _password = password;
            _baseDir = baseDir;
        }

        public void EnsureConnected()
        {
            if (_client== null)
            {
                _client = new SftpClient(_host, _username, _password);
            }

            if (!_client.IsConnected)
            {
                _client.Connect();
            }
        }

        public byte[] Download(string path)
        {
            var fullPath = Path.Combine(_baseDir, path).Replace("\\", "/"); ;
            EnsureConnected();
            return _client.ReadAllBytes(fullPath);
        }

        public bool Upload(string path, byte[] content, bool allowOverwrite = false)
        {
            var fullPath = Path.Combine(_baseDir, path).Replace("\\", "/");
            EnsureConnected();
            
            if (_client.Exists(fullPath))
            {
                if (allowOverwrite)
                {
                    _client.Delete(fullPath);
                }
                else
                {
                    return true;
                }
            }

            foreach (var directory in ((IFileStorage)this).GetParentDirectories(fullPath))
            {
                if (!_client.Exists(directory))
                    _client.CreateDirectory(directory);
            }
            using (var stream = _client.OpenWrite(fullPath))
            {
                stream.Write(content, 0, content.Length);
                return true;
            }
        }
    }
}
