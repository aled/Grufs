using System.Text.Json.Serialization;

namespace Wibblr.Grufs
{
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(SftpCredentials))]
    public sealed partial class SourceGenerationContext : JsonSerializerContext
    {
    }

    public class SftpCredentials
    {
        public string? Hostname { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
