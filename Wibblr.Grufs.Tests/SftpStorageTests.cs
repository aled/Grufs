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
        public string? Hostname { get; init; }
        public string? Username { get; init; }
        public string? Password { get; init; }
    }

    public class SftpStorageTests
    {
        [Fact]
        public void SftpUpload()
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var text = File.ReadAllText("sftp-credentials.json");

            var sftpCredentials = JsonSerializer.Deserialize<SftpCredentials>(text, options) ?? throw new Exception("Error deserializing SFTP credentials");

            var storage = new SftpStorage(
                sftpCredentials.Hostname ?? throw new Exception("Invalid SFTP hostname"), 
                sftpCredentials.Username ?? throw new Exception("Invalid SFTP username"), 
                sftpCredentials.Password ?? throw new Exception("Invalid SFTP password"));

            storage.Upload("tests/00001/test00001", Encoding.ASCII.GetBytes("Hello world!"));

            Console.WriteLine(Encoding.ASCII.GetString(storage.Download("tests/00001/test00001")));
        }
    }
}
