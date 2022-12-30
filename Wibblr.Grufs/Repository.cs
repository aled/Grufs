using System;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using Wibblr.Grufs.Encryption;

using static System.Runtime.InteropServices.JavaScript.JSType;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Wibblr.Grufs.Tests")]

namespace Wibblr.Grufs
{
    public class Repository
    {
        // Metadata does not need to be encrypted, but encrypt it anyway so that all files in the repository look the same
        // and can be copied without any special cases.
        //
        // Either use a well known password that provides no security whatsoever, or use a second password.
        // 
        // By using different metadata passwords, it should be possible to store multiple repositories in the same storage,
        // because the metadata will be stored at different addresses. Data will be deduplicated if the same file is in multiple
        // repositories.
        private const string _defaultRepositoryName = "";
        private static readonly Salt wellKnownSalt0 = new Salt(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
        private static readonly Salt wellKnownSalt1 = new Salt(new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 });

        private static readonly int _chunkSize = 128 * 1024;
        private IChunkStorage _chunkStorage;
        private StreamStorage _streamStorage;

        internal KeyEncryptionKey _masterKey;
        internal HmacKey _masterContentAddressKey;
        internal HmacKey _masterDictionaryAddressKey;

        public Repository(IChunkStorage chunkStorage) 
        { 
            _chunkStorage = chunkStorage;
            _streamStorage = new StreamStorage(chunkStorage, _chunkSize);
        }

        public bool Initialize(string password, string repositoryName = _defaultRepositoryName)
        {
            // The master keys required. Each chunk is encrypted with a random key, which is wrapped using the masterKey.
            // Additionally the address of chunks is computed using the addressKey (which is the same for all chunks)
            var masterKey = KeyEncryptionKey.Random();
            var contentAddressKey = HmacKey.Random();
            var dictionaryAddressKey = HmacKey.Random();

            // Encrypt the master keys using a key derived from the password
            var serializationVersion = (byte)0;
            var masterKeys = new BufferBuilder(1 + KeyEncryptionKey.Length + HmacKey.Length + HmacKey.Length + HmacKey.Length)
                .AppendByte(serializationVersion)
                .AppendBytes(masterKey.ToSpan())
                .AppendBytes(contentAddressKey.ToSpan())
                .AppendBytes(dictionaryAddressKey.ToSpan())
                .ToSpan();

            var normalizedPassword = Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormC));
            var salt = Salt.Random();
            var iterations = 500000;
            var masterKeysKey = new EncryptionKey(new Rfc2898DeriveBytes(normalizedPassword, salt.ToSpan().ToArray(), iterations, HashAlgorithmName.SHA256).GetBytes(EncryptionKey.Length));

            var encryptor = new Encryptor();
            var masterKeysInitializationVector = InitializationVector.Random();
            var encryptedMasterKeys = encryptor.Encrypt(masterKeys, masterKeysInitializationVector, masterKeysKey);

            var repositoryMetadata = new RepositoryMetadata(masterKeysInitializationVector, salt, iterations, encryptedMasterKeys);

            // Finally store the metadata using the DictionaryStorage. This will encrypt with either a well-known default or custom repository name. This is not necessary for security, but is there to make all the chunks in the repository look the same.
            // Note there is no random salt in this usage of the key derivation function as it depends on the password alone (otherwise the metadata could not be located)
            var normalizedRepositoryName = Encoding.UTF8.GetBytes(repositoryName.Normalize(NormalizationForm.FormC));
            var metadataKeyEncryptionKey = new KeyEncryptionKey(new Rfc2898DeriveBytes(normalizedRepositoryName, wellKnownSalt0.ToSpan().ToArray(), iterations, HashAlgorithmName.SHA256).GetBytes(KeyEncryptionKey.Length));
            var metadataAddressKey = new HmacKey(new Rfc2898DeriveBytes(normalizedRepositoryName, wellKnownSalt1.ToSpan().ToArray(), iterations, HashAlgorithmName.SHA256).GetBytes(KeyEncryptionKey.Length));

            if (!new UnversionedDictionaryStorage(_chunkStorage).TryPutValue(metadataKeyEncryptionKey, metadataAddressKey, Encoding.ASCII.GetBytes("metadata"), repositoryMetadata.Serialize(), OverwriteStrategy.DenyWithError))
            {
                throw new Exception("Error initializing repository - already exists");
            }

            _masterKey = masterKey;
            _masterContentAddressKey = contentAddressKey;
            _masterDictionaryAddressKey = dictionaryAddressKey;

            return true;
        }

        public bool Open(string password, string metadataPassword = _defaultRepositoryName)
        {
            // Get the serialized metadata from the dictionary storage. Note the encryption used for this is weak as it uses well known salts and probably a well known password.
            // The keys embedded in the metadata are wrapped with another layer of (strong) encryption.
            var normalizedMetadataPassword = Encoding.UTF8.GetBytes(metadataPassword.Normalize(NormalizationForm.FormC));
            var iterations = 500000;
            var metadataAddressKey = new HmacKey(new Rfc2898DeriveBytes(normalizedMetadataPassword, wellKnownSalt1.ToSpan().ToArray(), iterations, HashAlgorithmName.SHA256).GetBytes(KeyEncryptionKey.Length));
            var metadataKeyEncryptionKey = new KeyEncryptionKey(new Rfc2898DeriveBytes(normalizedMetadataPassword, wellKnownSalt0.ToSpan().ToArray(), iterations, HashAlgorithmName.SHA256).GetBytes(KeyEncryptionKey.Length));

            if (!new UnversionedDictionaryStorage(_chunkStorage).TryGetValue(metadataKeyEncryptionKey, metadataAddressKey, Encoding.ASCII.GetBytes("metadata"), out var serialized))
            {
                throw new Exception();
            }

            // Deserialize
            var metadata = new RepositoryMetadata(serialized);

            // Now decrypt the embedded keys in the metadata
            var encryptor = new Encryptor(); 
            var normalizedPassword = Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormC));
            var masterKeysKey = new EncryptionKey(new Rfc2898DeriveBytes(normalizedPassword, metadata.Salt.ToSpan().ToArray(), metadata.Iterations, HashAlgorithmName.SHA256).GetBytes(EncryptionKey.Length));
            var masterKeys = encryptor.Decrypt(metadata.EncryptedMasterKeys, metadata.MasterKeysInitializationVector, masterKeysKey);

            var serializationVersion = masterKeys[0];
            if (serializationVersion != 0)
            {
                throw new Exception("Invalid metadata");
            }

            _masterKey = new KeyEncryptionKey(masterKeys.Slice(1, KeyEncryptionKey.Length));
            _masterContentAddressKey = new HmacKey(masterKeys.Slice(1+ KeyEncryptionKey.Length, HmacKey.Length));
            _masterDictionaryAddressKey = new HmacKey(masterKeys.Slice(1 + KeyEncryptionKey.Length + HmacKey.Length, HmacKey.Length));

            return true;
        }

        private ReadOnlySpan<byte> GetDirectoryDictionaryKey(string path) => Encoding.UTF8.GetBytes("directory:" + path);

        private (RepositoryDirectory?, long version) GetLatestRepositoryDirectory(RepositoryDirectoryPath path, long hintVersion = 0)
        {
            var dictionaryKey = GetDirectoryDictionaryKey(path.CanonicalPath);
            var dictionaryStorage = new VersionedDictionaryStorage(_chunkStorage);

            var nextVersion = dictionaryStorage.GetNextSequenceNumber(_masterDictionaryAddressKey, dictionaryKey, hintVersion);

            if (nextVersion == 0)
            {
                return (null, 0);
            }

            var currentVersion = nextVersion - 1;
            if (dictionaryStorage.TryGetValue(_masterKey, _masterDictionaryAddressKey, dictionaryKey, currentVersion, out var buffer))
            {
                return (new RepositoryDirectory(new BufferReader(buffer)), currentVersion);
            }

            throw new Exception("Missing directory version");
        }

        private (RepositoryDirectory, long version) WriteRepositoryDirectoryVersion(RepositoryDirectory directory, long version)
        {
            var serialized = new BufferBuilder(directory.GetSerializedLength()).AppendRepositoryDirectory(directory).ToBuffer();
            var lookupKey = GetDirectoryDictionaryKey(directory.Path.CanonicalPath);
            new VersionedDictionaryStorage(_chunkStorage).TryPutValue(_masterKey, _masterDictionaryAddressKey, lookupKey, version, serialized.AsSpan());
            return (directory, version);
        }

        private (RepositoryDirectory, long version) EnsureDirectoryContains(RepositoryDirectoryPath repositoryDirectoryPath, RepositoryFilename childDirectory, long parentVersion)
        {
            var (repositoryDirectory, version) = GetLatestRepositoryDirectory(repositoryDirectoryPath);

            // If directory exists but the latest version does not contain the child directory, then update directory to contain the new child 
            if (repositoryDirectory != null)
            {
                if (repositoryDirectory.Directories.Contains(childDirectory))
                {
                    return (repositoryDirectory, version);
                }

                return WriteRepositoryDirectoryVersion(
                    repositoryDirectory with { Directories = repositoryDirectory.Directories.Add(childDirectory) },
                    version + 1);
            }

            // directory does not exist, create.
            return WriteRepositoryDirectoryVersion(
                new RepositoryDirectory(repositoryDirectoryPath, parentVersion, Timestamp.Now, false, new RepositoryFile[0], new RepositoryFilename[] { childDirectory }), 
                0);
        }

        // Non recursive upload of a local directory to the repository
        public (RepositoryDirectory, long version) UploadDirectoryNonRecursive(string localDirectoryPath, RepositoryDirectoryPath repositoryDirectoryPath)
        {
            long parentVersion = 0; // default value for root directory

            // Starting at the root and going down to the parent of this directory, ensure each directory exists and contains the child directory
            // Not we do not go down to this directory, as it also needs the files before we write it.
            foreach (var (dirPath, childDirName) in repositoryDirectoryPath.PathHierarchy())
            {
                // ensure that dir exists, and contains childDir
                (_, parentVersion) = EnsureDirectoryContains(dirPath, childDirName, parentVersion);
            }

            // Upload all local files, recording the address/chunk type/other metadata of each
            // Ignore directories.
            var filesBuilder = ImmutableArray.CreateBuilder<RepositoryFile>();

            var di = new DirectoryInfo(localDirectoryPath);
            if (!di.Exists)
            {
                throw new Exception("Invalid directory");
            }

            foreach (var file in di.EnumerateFiles())
            {
                using (var stream = new FileStream(file.FullName, FileMode.Open))
                {
                    var (address, chunkType) = _streamStorage.Write(_masterKey, _masterContentAddressKey, stream);
                    filesBuilder.Add(new RepositoryFile(new RepositoryFilename(file.Name), address, chunkType, new Timestamp(file.LastWriteTimeUtc)));
                }
            }

            // finally upload this directory
            var (directory, version) = GetLatestRepositoryDirectory(repositoryDirectoryPath);

            if (directory == null)
            {
                return WriteRepositoryDirectoryVersion(new RepositoryDirectory(repositoryDirectoryPath, parentVersion, new Timestamp(di.LastWriteTimeUtc), false, filesBuilder.ToImmutableArray(), new RepositoryFilename[0]), 0);
            }

            var oldFiles = directory.Files.Except(filesBuilder);
            filesBuilder.AddRange(oldFiles);

            var updated = directory with { Files = filesBuilder.ToImmutableArray() };
            return WriteRepositoryDirectoryVersion(updated, version + 1);
        }
    }
}