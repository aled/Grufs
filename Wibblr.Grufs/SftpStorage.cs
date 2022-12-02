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
    public class SftpStorage : IFileStorage
    {
        private string _host, _username, _password, _baseDir;

        public SftpStorage(string host, string username, string password, string baseDir)
        {
            _host = host;
            _username = username;
            _password = password;
            _baseDir = baseDir;
        }

        public byte[] Download(string path)
        {
            var fullPath = Path.Combine(_baseDir, path).Replace("\\", "/"); ;

            using (var client = new SftpClient(_host, _username, _password))
            {
                client.Connect();
                return client.ReadAllBytes(fullPath);
            }
        }

        public void Upload(string path, byte[] content)
        {
            var fullPath = Path.Combine(_baseDir, path).Replace("\\", "/");

            using (var client = new SftpClient(_host, _username, _password))
            {
                client.Connect();

                if (!client.Exists(fullPath))
                {
                    foreach (var directory in ((IFileStorage)this).GetParentDirectories(fullPath))
                    {
                        if (!client.Exists(directory))
                            client.CreateDirectory(directory);
                    }
                    using (var stream = client.OpenWrite(fullPath))
                    {
                        stream.Write(content, 0, content.Length);
                    }
                }
            }
        }
    }
}
