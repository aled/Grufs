using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;

using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Filesystem
{
    public record struct DirectoryPath
    {
        public required string NormalizedPath { get; init; }
        public string CanonicalPath => NormalizedPath.ToLowerInvariant();

        [SetsRequiredMembers]
        public DirectoryPath(string path)
        {
            ArgumentNullException.ThrowIfNull(path);

            NormalizedPath = Normalize(path);
        }

        [SetsRequiredMembers]
        public DirectoryPath(BufferReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);

            var path = reader.ReadString();

            NormalizedPath = Normalize(path);
        }

        public int GetSerializedLength()
        {
            return NormalizedPath.GetSerializedLength();
        }

        public void SerializeTo(BufferBuilder builder)
        {
            builder.AppendString(NormalizedPath);
        }

        private static string Normalize(string s)
        {
            ArgumentNullException.ThrowIfNull(s);

            s = s.IsNormalized() ? s : s.Normalize(NormalizationForm.FormC);
            s = s.Replace('\\', '/');

            var parts = s.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Contains(".") || parts.Contains(".."))
            {
                throw new ArgumentException($"Invalid directory path: {s}");
            }

            return string.Join('/', parts);
        }

        public DirectoryPath Parent()
        {
            var index = NormalizedPath.LastIndexOf('/');
            if (index >= 0)
            {
                return new DirectoryPath(NormalizedPath.Substring(0, index));
            }
            return new DirectoryPath("");
        }

        public IEnumerable<(DirectoryPath, Filename)> PathHierarchy()
        {
            var parts = NormalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length > 0) 
            {
                yield return (new DirectoryPath(""), new Filename(parts[0]));
            }

            for (int i = 1; i < parts.Length; i++) 
            { 
                yield return (new DirectoryPath(string.Join("/", parts.Take(i))), new Filename(parts[i]));
            }
        }

        public override string ToString() => NormalizedPath;
    }
}
