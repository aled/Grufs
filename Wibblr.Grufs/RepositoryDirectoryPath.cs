using System;

namespace Wibblr.Grufs
{
    public record struct RepositoryDirectoryPath
    {
        public string NormalizedPath { get; private init; }
        public string CanonicalPath => NormalizedPath.ToLowerInvariant();

        public RepositoryDirectoryPath(BufferReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);

            NormalizedPath = reader.ReadString();
            
            if (NormalizedPath != Normalize(NormalizedPath))
            {
                throw new ArgumentException("Invalid serialized RepositoryDirectoryPath: expected normalized value but is not normalized");
            }
        }

        public RepositoryDirectoryPath(string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            NormalizedPath = Normalize(value);
        }

        private static string Normalize(string s)
        {
            ArgumentNullException.ThrowIfNull(s);

            s = s.IsNormalized() ? s : s.Normalize();
            s = s.Replace("\\", "/");

            var parts = s.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Contains(".") || parts.Contains(".."))
            {
                throw new ArgumentException($"Invalid directory path: {s}");
            }

            return string.Join("/", parts);
        }

        public RepositoryDirectoryPath Parent()
        {
            var index = CanonicalPath.LastIndexOf("/");
            if (index >= 0)
            {
                return new RepositoryDirectoryPath(CanonicalPath.Substring(0, index));
            }
            return new RepositoryDirectoryPath("");
        }

        public (RepositoryDirectoryPath, RepositoryFilename) ParentAndName()
        {
            var parts = NormalizedPath.Split('/');

            if (parts.Length == 0)
            {
                throw new Exception();
            }
                
            return (new RepositoryDirectoryPath(string.Join("/", parts.Take(parts.Length - 1))), new RepositoryFilename(parts.Last()));
        }

        public IEnumerable<(RepositoryDirectoryPath, RepositoryFilename)> PathHierarchy()
        {
            var parts = NormalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length > 0) 
            {
                yield return (new RepositoryDirectoryPath(""), new RepositoryFilename(parts[0]));
            }

            for (int i = 1; i < parts.Length; i++) 
            { 
                yield return (new RepositoryDirectoryPath(string.Join("/", parts.Take(i))), new RepositoryFilename(parts[i]));
            }
        }

        public int GetSerializedLength()
        {
            return new VarInt(NormalizedPath.Length).GetSerializedLength() + (sizeof(char) * NormalizedPath.Length);
        }

        public void SerializeTo(BufferBuilder builder)
        {
            builder.AppendString(NormalizedPath);
        }

        public override string ToString() => NormalizedPath;
    }
}
