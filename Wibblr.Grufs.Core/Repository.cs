using System;
using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

using Wibblr.Grufs.Encryption;
using Wibblr.Grufs.Storage;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Wibblr.Grufs.Tests")]

namespace Wibblr.Grufs.Core
{
    public enum OpenRepositoryStatus
    {
        Unknown = 0,
        Success = 1,
        MissingMetadata = 2,
        InvalidMetadata = 3,
        BadPassword = 4,
    }

    public enum InitRepositoryStatus
    {
        Unknown = 0,
        Success = 1,
        AlreadyExists = 2,
        Error = 3,
    }

    public record struct OpenRepositoryResult(OpenRepositoryStatus Status, string Message);
    public record struct InitRepositoryResult(InitRepositoryStatus Status, string Message);

    /// <summary>
    /// The Repository specifies the encryption key and address key for all content within the repository. Anything written to storage that
    /// uses the same repository will be deduplicated against all existing files in that repository.
    /// 
    /// To access a repository, a password and repository name is required.
    /// 
    /// Backup sets are created within a repository. To create, the repository name/password AND the backup set name/password is
    /// required.
    /// 
    /// 
    /// </summary>
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
        private const string _defaultMetadataPassword = "[default]";
        private const string _repositoryKeyNamespace = "repository:";

        private static readonly ImmutableArray<byte> _metadataLookupKey = Encoding.ASCII.GetBytes("metadata:").ToImmutableArray();

        private static readonly Salt wellKnownSalt0 = new Salt(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
        private static readonly Salt wellKnownSalt1 = new Salt(new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 });
       
        /// <summary>
        /// Name of repository. Not used for any encryption or hashing purposes, this is simply a name supplied
        /// by the user to identify the rep.
        /// </summary>
        public string Name { get; private set; }

        public IChunkStorage ChunkStorage { get; private set; }

        public string EncryptionPassword { get; private set; }

        public KeyEncryptionKey MasterKey { get; private set; }

        public HmacKey MasterContentAddressKey { get; private set; }

        public HmacKey VersionedDictionaryAddressKey { get; private set; }

        public HmacKey UnversionedDictionaryAddressKey { get; private set; }


        private StreamStorage? _streamStorage;
        public StreamStorage StreamStorage { get => _streamStorage ?? throw new NullReferenceException(); }
     

        private Compressor? _compressor;
        public Compressor Compressor { get => _compressor ?? throw new NullReferenceException(); }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="chunkStorage">The storage used by the repository</param>
        /// <param name="encryptionPassword">The password used to encrypt the repository</param>
        public Repository(string name, IChunkStorage chunkStorage, string encryptionPassword)
        {
            Name = name;
            ChunkStorage = chunkStorage;
            EncryptionPassword = encryptionPassword;
        }

        /// <summary>
        /// Initialize repository. This consists of writing the repository metadata
        /// </summary>
        /// <param name="compressor">Object that defines the compression algorithm and level. Defaults to Brotli.</param>
        /// <returns></returns>
        public Task<InitRepositoryResult> InitializeAsync(CancellationToken token)
        {
            return InitializeAsync(new Compressor(CompressionAlgorithm.Brotli, CompressionLevel.Optimal), token);
        }

        public async Task<InitRepositoryResult> InitializeAsync(Compressor compressor, CancellationToken token)
        {
            _compressor = compressor;

            // The master keys required. Each chunk is encrypted with a random key, which is wrapped using the masterKey.
            // Additionally the address of chunks is computed using the addressKey (which is the same for all chunks)
            var masterKey = KeyEncryptionKey.Random();
            var contentAddressKey = HmacKey.Random();
            var versionedDictionaryAddressKey = HmacKey.Random();
            var unversionedDictionaryAddressKey = HmacKey.Random();

            // Encrypt the master keys using a key derived from the password
            var serializationVersion = (byte)0;
            var masterKeys = new BufferBuilder(1 + 1 + 1 + KeyEncryptionKey.Length + HmacKey.Length + HmacKey.Length + HmacKey.Length)
                .AppendByte(serializationVersion)
                .AppendByte((byte)_compressor.Algorithm)
                .AppendByte((byte)_compressor.Level)
                .AppendKeyEncryptionKey(masterKey)
                .AppendHmacKey(contentAddressKey)
                .AppendHmacKey(versionedDictionaryAddressKey)
                .AppendHmacKey(unversionedDictionaryAddressKey)
                .ToBuffer();

            var normalizedPassword = Encoding.UTF8.GetBytes(EncryptionPassword.Normalize(NormalizationForm.FormC));
            var salt = Salt.Random();
            var iterations = 500000;
            var masterKeysKey = new EncryptionKey(new Rfc2898DeriveBytes(normalizedPassword, salt.ToSpan().ToArray(), iterations, HashAlgorithmName.SHA256).GetBytes(EncryptionKey.Length));

            var encryptor = new Encryptor();
            var masterKeysInitializationVector = InitializationVector.Random();
            var encryptedMasterKeys = encryptor.Encrypt(masterKeys.AsSpan(), masterKeysInitializationVector, masterKeysKey);

            var repositoryMetadata = new RepositoryMetadata(masterKeysInitializationVector, salt, iterations, encryptedMasterKeys);

            // Finally store the metadata using the DictionaryStorage. This will encrypt with a well-known default name. This does not provide any security, but is there to make all the chunks
            // in the repository look the same, so that no special cases are needed for copying chunks around.
            // Note there is no random salt in this usage of the key derivation function as it depends on the password alone (otherwise the metadata could not be located)
            // Although the metadata can be trivially decrypted, the actual keys have another layer of proper encryption.
            var normalizedMetadataPassword = Encoding.UTF8.GetBytes(_defaultMetadataPassword.Normalize(NormalizationForm.FormC));
            var metadataKeyEncryptionKey = new KeyEncryptionKey(new Rfc2898DeriveBytes(normalizedMetadataPassword, wellKnownSalt0.ToSpan().ToArray(), iterations, HashAlgorithmName.SHA256).GetBytes(KeyEncryptionKey.Length));
            var metadataAddressKey = new HmacKey(new Rfc2898DeriveBytes(normalizedMetadataPassword, wellKnownSalt1.ToSpan().ToArray(), iterations, HashAlgorithmName.SHA256).GetBytes(KeyEncryptionKey.Length));
            var metadataChunkEncryptor = new ChunkEncryptor(metadataKeyEncryptionKey, metadataAddressKey, Compressor.None);

            await ChunkStorage.InitAsync(token);

            var putResult = await new UnversionedDictionary(ChunkStorage, metadataChunkEncryptor).PutValueAsync(_metadataLookupKey, repositoryMetadata.Serialize(), OverwriteStrategy.Deny, token);
            if (putResult == PutStatus.OverwriteDenied)
            {
                return new InitRepositoryResult(InitRepositoryStatus.AlreadyExists, "Repository already exists");
            }
            if (putResult == PutStatus.Error)
            {
                return new InitRepositoryResult(InitRepositoryStatus.Error, "Error");
            }

            MasterKey = masterKey;
            MasterContentAddressKey = contentAddressKey;
            VersionedDictionaryAddressKey = versionedDictionaryAddressKey;
            UnversionedDictionaryAddressKey = unversionedDictionaryAddressKey;

            // metadata itself is always stored uncompressed
            var chunkEncryptor = new ChunkEncryptor(MasterKey, MasterContentAddressKey, new Compressor(CompressionAlgorithm.None));
            var chunkSourceFactory = new ContentDefinedChunkSourceFactory(13);

            _streamStorage = new StreamStorage(ChunkStorage, chunkSourceFactory, chunkEncryptor);

            ChunkStorage.Flush();

            return new InitRepositoryResult(InitRepositoryStatus.Success, "OK");
        }

        public async Task<OpenRepositoryResult> OpenAsync(CancellationToken token)
        {
            // Get the serialized metadata from the dictionary storage. Note the encryption used for this is weak as it uses well known salts and probably a well known password.
            // The keys embedded in the metadata are wrapped with another layer of (strong) encryption.
            var normalizedMetadataPassword = Encoding.UTF8.GetBytes(_defaultMetadataPassword.Normalize(NormalizationForm.FormC));
            var iterations = 500000;
            var metadataAddressKey = new HmacKey(new Rfc2898DeriveBytes(normalizedMetadataPassword, wellKnownSalt1.ToSpan().ToArray(), iterations, HashAlgorithmName.SHA256).GetBytes(KeyEncryptionKey.Length));
            var metadataKeyEncryptionKey = new KeyEncryptionKey(new Rfc2898DeriveBytes(normalizedMetadataPassword, wellKnownSalt0.ToSpan().ToArray(), iterations, HashAlgorithmName.SHA256).GetBytes(KeyEncryptionKey.Length));
            var metadataChunkEncryptor = new ChunkEncryptor(metadataKeyEncryptionKey, metadataAddressKey, Compressor.None);

            var unversionedDictionary = new UnversionedDictionary(ChunkStorage, metadataChunkEncryptor);

            var serialized = await unversionedDictionary.GetValueAsync(_metadataLookupKey, token);
            if (serialized == null)
            {
                return new OpenRepositoryResult(OpenRepositoryStatus.MissingMetadata, $"Unable to find metadata '{_defaultMetadataPassword}' in storage");
            }

            // Deserialize
            var metadata = new RepositoryMetadata(serialized);

            // Now decrypt the embedded keys in the metadata
            var encryptor = new Encryptor(); 
            var normalizedPassword = Encoding.UTF8.GetBytes(EncryptionPassword.Normalize(NormalizationForm.FormC));
            var masterKeysKey = new EncryptionKey(new Rfc2898DeriveBytes(normalizedPassword, metadata.Salt.ToSpan().ToArray(), metadata.Iterations, HashAlgorithmName.SHA256).GetBytes(EncryptionKey.Length));

            // this will fail to decrypt if the entered password is incorrect.
            try
            {
                var (decryptedBuffer, decryptedCount) = encryptor.Decrypt(metadata.EncryptedMasterKeys, metadata.MasterKeysInitializationVector, masterKeysKey);

                var masterKeys = new BufferReader(new ArrayBuffer(decryptedBuffer, decryptedCount));

                var serializationVersion = masterKeys.ReadByte();
                if (serializationVersion != 0)
                {
                    return new OpenRepositoryResult(OpenRepositoryStatus.InvalidMetadata, $"Invalid serialization version '{serializationVersion}' in metadata");
                }

                var compressionAlgorithm = (CompressionAlgorithm)masterKeys.ReadByte();
                var compressionLevel = (CompressionLevel)masterKeys.ReadByte();
                _compressor = new Compressor(compressionAlgorithm, compressionLevel);

                MasterKey = masterKeys.ReadKeyEncryptionKey();
                MasterContentAddressKey = masterKeys.ReadHmacKey();
                VersionedDictionaryAddressKey = masterKeys.ReadHmacKey();
                UnversionedDictionaryAddressKey = masterKeys.ReadHmacKey();

                var chunkEncryptor = new ChunkEncryptor(MasterKey, MasterContentAddressKey, Compressor);
                var chunkSourceFactory = new ContentDefinedChunkSourceFactory(13);

                _streamStorage = new StreamStorage(ChunkStorage, chunkSourceFactory, chunkEncryptor);
            }
            catch(Exception)
            {
                return new OpenRepositoryResult(OpenRepositoryStatus.BadPassword, $"Unable to decrypt metadata with given password");
            }
            return new OpenRepositoryResult(OpenRepositoryStatus.Success, "OK");
        }

        public Collection GetCollectionStorage(string collectionName)
        {
            return new Collection(new VersionedDictionary("collection:", ChunkStorage, new ChunkEncryptor(MasterKey, VersionedDictionaryAddressKey, Compressor.None)), collectionName);
        }

        /// <summary>
        /// A list of all immutable filesystems is stored in the repository using the versioned dictionary storage with a random address key. Each 
        /// </summary>
        /// <returns></returns>
        public async Task<List<string>> ListBackupSetsAsync(CancellationToken token)
        {
            // Each version of the value in the dictionary is a changeset that needs to be applied to get the total list.
            var chunkEncryptor = new ChunkEncryptor(MasterKey, VersionedDictionaryAddressKey, Compressor.None);
            var dict = new VersionedDictionary(_repositoryKeyNamespace, ChunkStorage, chunkEncryptor);
            var lookupKey = Encoding.UTF8.GetBytes("backupSets:");

            var sequenceNumber = 0L;
            while (await dict.GetValueAsync(lookupKey, sequenceNumber++, token) != null)
            {
                // Buffer contains lines of the form:
                // CREATE:client:clientdir:name:timestamp
            }

            throw new NotImplementedException();
        }
    }
}