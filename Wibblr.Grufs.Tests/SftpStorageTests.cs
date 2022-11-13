using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Wibblr.Grufs.Tests
{
    internal class SftpCredentials
    {
        public string Hostname { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class SftpStorageTests
    {
        [Fact]
        public void SftpUpload()
        {
            var sftpCredentials = JsonSerializer.Deserialize<SftpCredentials>(File.ReadAllText("sftp-credentials.json"), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); ;

            var storage = new SftpStorage(sftpCredentials.Hostname, sftpCredentials.Username, sftpCredentials.Password);

            storage.Upload("tests/00001/test00001", Encoding.ASCII.GetBytes("Hello world!"));

            Console.WriteLine(Encoding.ASCII.GetString(storage.Download("tests/00001/test00001")));
        }
    }
}
