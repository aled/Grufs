using System;

namespace Wibblr.Grufs
{
    /// <summary>
    /// Filenames within the repository are not subject to any restrictions on permitted characters, except that the
    /// forward slash character is not permitted.
    /// 
    /// They will be translated to valid filenames on the target filesystem as required. The original name
    /// from the source filesystem will be retained so that changes to the file will work as expected
    /// regardless of whether the client filesystem supports the original name.
    /// 
    /// For example:
    /// 
    /// Linux file with Name = 'A:B?C' => 
    ///   Repository file with OriginalName = 'A:B?C' and CanonicalName 'a:b?c' => 
    ///     Windows file with OriginalName = 'A:B?C', CanonicalName = 'a:b?c' and WindowsName = A%3AB%3CC
    /// </summary>
    public record struct RepositoryFilename
    {
        public string OriginalName { get; private init; }
        public string CanonicalName { get; private init; } // The name used for hashing. Normalized and case-insensitive.

        public RepositoryFilename(BufferReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);

            OriginalName = reader.ReadString();
            CanonicalName = reader.ReadString();

            if (CanonicalName != Canonicalize(OriginalName))
            {
                throw new ArgumentException($"Canonical name is not valid (actual {CanonicalName}, expected {Canonicalize(OriginalName)})");
            }

            Validate(OriginalName);
            Validate(CanonicalName);
        }

        public RepositoryFilename(string value)
        {
            ArgumentNullException.ThrowIfNull(value, nameof(value));

            OriginalName = value;
            CanonicalName = Canonicalize(value);

            Validate(OriginalName);
            Validate(CanonicalName);
        }

        private static void Validate(string s)
        {
            if (s == "." || s == ".." || s.Contains('/'))
            {
                throw new ArgumentException("Filename may not contain forward slash character");
            }
        }   

        private static string Canonicalize(string s)
        {
            return (s.IsNormalized() ? s : s.Normalize()).ToLowerInvariant();
        }

        public static implicit operator string(RepositoryFilename repositoryFilename) => repositoryFilename.OriginalName;

        public int GetSerializedLength()
        {
            return new VarInt(OriginalName.Length).GetSerializedLength() + (sizeof(char) * OriginalName.Length) + 
                   new VarInt(CanonicalName.Length).GetSerializedLength() + (sizeof(char) * CanonicalName.Length);
        }

        public void SerializeTo(BufferBuilder builder)
        {
            builder.AppendString(OriginalName);
            builder.AppendString(CanonicalName);
        }

        public override string ToString() => OriginalName;
    }
}
