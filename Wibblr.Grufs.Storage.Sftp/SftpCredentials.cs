using System.Text.Json.Serialization;

namespace Wibblr.Grufs.Storage.Sftp
{
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(SftpCredentials))]
    public sealed partial class SourceGenerationContext : JsonSerializerContext
    {
    }

    public class SftpCredentials
    {
        public string? Host { get; set; }
        public int Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? PrivateKey { get; set; }
    }
}
