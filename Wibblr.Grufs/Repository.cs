using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings;

using Wibblr.Grufs.Encryption;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Wibblr.Grufs.Tests")]

namespace Wibblr.Grufs
{
    public class Repository
    {
        // Metadata does not need to be encrypted, but encrypt it anyway so that all files in the repository look the same
        // and can be copied without any special cases.
        //
        // Either use a well known password that provides no security whatsoever, or use a second password
        private const string _wellKnownMetadataPassword = "This password is used to encrypt and generate the address of the repository metadata. The security of the system does not depend on this being secret.";
        private static readonly Salt wellKnownSalt0 = new Salt(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
        private static readonly Salt wellKnownSalt1 = new Salt(new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 });

        private static readonly int _chunkSize = 128 * 1024;
        private IChunkStorage _chunkStorage;
        private StreamStorage _streamStorage;

        internal KeyEncryptionKey _masterEncryptionKey;
        internal HmacKey _masterAddressKey;

        public Repository(IChunkStorage chunkStorage) 
        { 
            _chunkStorage = chunkStorage;
            _streamStorage = new StreamStorage(chunkStorage, _chunkSize);
        }

        public bool Initialize(string password, string metadataPassword = _wellKnownMetadataPassword)
        {
            // The master keys required. Each chunk is encrypted with a random key, which is wrapped using the masterKey.
            // Additionally the address of chunks is computed using the addressKey (which does not change)
            var masterKey = KeyEncryptionKey.Random();
            var addressKey = HmacKey.Random();

            // Encrypt the master keys using a key derived from the password
            var serializationVersion = (byte)0;
            var masterKeys = new Buffer(1 + KeyEncryptionKey.Length + HmacKey.Length)
                .Append(serializationVersion)
                .Append(masterKey.ToSpan())
                .Append(addressKey.ToSpan());

            var normalizedPassword = Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormC));
            var salt = Salt.Random();
            var iterations = 500000;
            var masterKeysKey = new EncryptionKey(new Rfc2898DeriveBytes(normalizedPassword, salt.ToSpan().ToArray(), iterations, HashAlgorithmName.SHA256).GetBytes(EncryptionKey.Length));

            var encryptor = new Encryptor();
            var masterKeysInitializationVector = InitializationVector.Random();
            var encryptedMasterKeys = encryptor.Encrypt(masterKeys.ToSpan(), masterKeysInitializationVector, masterKeysKey);

            var repositoryMetadata = new RepositoryMetadata(masterKeysInitializationVector, salt, iterations, encryptedMasterKeys);

            // Finally store the metadata using the DictionaryStorage. This will encrypt with either a well-known or custom password. This is not necessary for security, but is there to make all the chunks in the repository look the same.
            // Note there is no random salt in this usage of the key derivation function as it depends on the password alone (otherwise the metadata could not be located)
            var normalizedMetadataPassword = Encoding.UTF8.GetBytes(metadataPassword.Normalize(NormalizationForm.FormC));
            var metadataKeyEncryptionKey = new KeyEncryptionKey(new Rfc2898DeriveBytes(normalizedMetadataPassword, wellKnownSalt0.ToSpan().ToArray(), iterations, HashAlgorithmName.SHA256).GetBytes(KeyEncryptionKey.Length));
            var metadataAddressKey = new HmacKey(new Rfc2898DeriveBytes(normalizedMetadataPassword, wellKnownSalt1.ToSpan().ToArray(), iterations, HashAlgorithmName.SHA256).GetBytes(KeyEncryptionKey.Length));

            if (!new DictionaryStorage(_chunkStorage).TryPutValue(metadataKeyEncryptionKey, metadataAddressKey, Encoding.ASCII.GetBytes("metadata"), repositoryMetadata.Serialize(), OverwriteStrategy.DenyWithError))
            {
                throw new Exception();
            }

            _masterEncryptionKey = masterKey;
            _masterAddressKey = addressKey;

            return true;
        }

        public bool Open(string password, string metadataPassword = _wellKnownMetadataPassword)
        {
            // Get the serialized metadata from the dictionary storage. Note the encryption used for this is weak as it uses well known salts and probably a well known password.
            // The keys embedded in the metadata are wrapped with another layer of (strong) encryption.
            var normalizedMetadataPassword = Encoding.UTF8.GetBytes(metadataPassword.Normalize(NormalizationForm.FormC));
            var iterations = 500000;
            var metadataAddressKey = new HmacKey(new Rfc2898DeriveBytes(normalizedMetadataPassword, wellKnownSalt1.ToSpan().ToArray(), iterations, HashAlgorithmName.SHA256).GetBytes(KeyEncryptionKey.Length));
            var metadataKeyEncryptionKey = new KeyEncryptionKey(new Rfc2898DeriveBytes(normalizedMetadataPassword, wellKnownSalt0.ToSpan().ToArray(), iterations, HashAlgorithmName.SHA256).GetBytes(KeyEncryptionKey.Length));

            if (!new DictionaryStorage(_chunkStorage).TryGetValue(metadataKeyEncryptionKey, metadataAddressKey, Encoding.ASCII.GetBytes("metadata"), out var serialized))
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

            _masterEncryptionKey = new KeyEncryptionKey(masterKeys.Slice(1, KeyEncryptionKey.Length));
            _masterAddressKey = new HmacKey(masterKeys.Slice(KeyEncryptionKey.Length, HmacKey.Length));

            return true;
        }

        public void Upload(string localDir, string repositoryDir)
        {
            var di = new DirectoryInfo(localDir);

            if (!di.Exists)
            {
                throw new Exception("Invalid directory");
            }

            // Upload all files
            var files = new List<GrufsFile>();

            var contentKeyEncryptionKey = new KeyEncryptionKey();
            var hmacKey = new HmacKey();


            //var wrappedHmacKey = hmacKey.Wrap(hmacKeyEncryptionKey);

            //foreach (var file in di.GetFiles())
            //{
            //    using (var stream = new FileStream(file.FullName, FileMode.Open))
            //    {
            //        _streamStorage.EncryptStream(contentKeyEncryptionKey, wrappedHmacKey, hmacKeyEncryptionKey, stream!, _chunkStorage);
            //    }
            //}

            //var gd = new GrufsDirectory
            //{
            //    Name = di.Name.Normalize(NormalizationForm.FormC),
            //    Files = di.EnumerateFiles().Select(x => new GrufsFile()).ToList()
            //};

            // Encrypt the directory info and upload!



        }
    }
}