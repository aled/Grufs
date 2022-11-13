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
        private string _host, _username, _password;

        public SftpStorage(string host, string username, string password)
        {
            _host = host;
            _username = username;
            _password = password;
        }

        public byte[] Download(string path)
        {
            using (var client = new SftpClient(_host, _username, _password))
            {
                client.Connect();
                return client.ReadAllBytes(path);
            }
        }

        public void Upload(string path, byte[] content)
        {
            using (var client = new SftpClient(_host, _username, _password))
            {
                client.Connect();

                if (!client.Exists(path))
                {
                    foreach (var directory in ((IFileStorage)this).GetParentDirectories(path))
                    {
                        if (!client.Exists(directory))
                            client.CreateDirectory(directory);
                    }
                    using (var stream = client.OpenWrite(path))
                    {
                        stream.Write(content, 0, content.Length);
                    }
                }
            }
        }
    }
}
