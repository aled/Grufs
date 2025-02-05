using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Wibblr.Grufs.Logging;

using static Wibblr.Grufs.Storage.FileStorageUtils;

namespace Wibblr.Grufs.Storage
{
    public class LocalStorage : IChunkStorage
    {
        public string BaseDir { get; init; }

        public LocalStorage(string baseDir)
        {
            BaseDir = Path.TrimEndingDirectorySeparator(baseDir);
        }

        public Task InitAsync(CancellationToken token)
        {
            try
            {
                Directory.CreateDirectory(BaseDir);
                return Task.CompletedTask;
            }
            catch (Exception)
            {
                if (Directory.Exists(BaseDir))
                {
                    return Task.CompletedTask;
                }
                throw new Exception("Error creating basedir");
            }
        }

        public Task<long> CountAsync(CancellationToken token)
        {
            return Task.FromResult((long)ListChunkFiles().Count());
        }

        public Task<bool> ExistsAsync(Address address, CancellationToken token)
        {
            return Task.FromResult(File.Exists(Path.Join(BaseDir, GeneratePath(address))));
        }

        public void Flush()
        {
            // no op
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async IAsyncEnumerable<Address> ListAddressesAsync([EnumeratorCancellation] CancellationToken token)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            foreach (var file in ListChunkFiles())
            {
                yield return new Address(Convert.FromHexString(file.Name));
            }
        }

        private async Task CreateParentsAndWriteAllBytesAsync(string path, byte[] content, CancellationToken token)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);

            try
            {
                var partFileName = path + ".part";
                using var partFile = File.OpenWrite(partFileName);

                await File.WriteAllBytesAsync(path, content);
            }
            catch (DirectoryNotFoundException)
            {
                var parent = Path.GetDirectoryName(path);
                if (parent != null)
                {
                    Directory.CreateDirectory(parent);
                    await File.WriteAllBytesAsync(path, content);
                }
            }
        }

        public async Task<PutStatus> PutAsync(EncryptedChunk chunk, OverwriteStrategy overwriteStrategy, CancellationToken token)
        {
            var path = Path.Join(BaseDir, GeneratePath(chunk.Address));

            switch (overwriteStrategy)
            {
                case OverwriteStrategy.Allow:
                    await CreateParentsAndWriteAllBytesAsync(path, chunk.Content, token);
                    return PutStatus.Success;

                case OverwriteStrategy.Deny:
                    if (File.Exists(path))
                    {
                        return PutStatus.OverwriteDenied;
                    }

                    await CreateParentsAndWriteAllBytesAsync(path, chunk.Content, token);
                    return PutStatus.Success;

                default:
                    throw new Exception("Invalid overwrite strategy");
            }
        }

        public async Task<EncryptedChunk?> GetAsync(Address address, CancellationToken token)
        {
            var path = Path.Join(BaseDir, GeneratePath(address));

            try
            {
                var content = await File.ReadAllBytesAsync(path, token);
                return new EncryptedChunk(address, content);
            }
            catch
            {
                return null;
            }
        }

        private IEnumerable<FileInfo> ListChunkFiles()
        {
            foreach (var grandparent in new DirectoryInfo(BaseDir).EnumerateDirectories())
            {
                if (grandparent.Name.Length == 2 && IsHexString(grandparent.Name))
                {
                    foreach (var parent in grandparent.EnumerateDirectories())
                    {
                        if (parent.Name.Length == 2 && IsHexString(grandparent.Name))
                        {
                            foreach (var address in parent.EnumerateFiles())
                            {
                                if (address.Name.Length == Address.Length * 2 && IsHexString(address.Name))
                                {
                                    yield return address;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
