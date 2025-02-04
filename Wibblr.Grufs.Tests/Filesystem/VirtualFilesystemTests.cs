using Wibblr.Grufs.Cli;
using Wibblr.Grufs.Core;
using Wibblr.Grufs.Filesystem;
using Wibblr.Grufs.Storage.Sqlite;

namespace Wibblr.Grufs.Tests
{
    public class VirtualFilesystemTests
    {
        [Fact]
        public void SyncDirectory()
        {
            using (var autoDeleteDirectory = new AutoDeleteDirectory())
            {
                var configDir = Path.Join(autoDeleteDirectory.Path, "config"); ;
                var baseDir = Path.Join(autoDeleteDirectory.Path, "repo");
                var contentDir = Path.Join(autoDeleteDirectory.Path, "content");
                var downloadDir = Path.Join(autoDeleteDirectory.Path, "download");

                Utils.CreateDirectoryTree(contentDir, "a.txt", "b/c.txt", "b/d.txt", "b/e/f.txt");

                using (var storage = new SqliteStorage(Path.Join(baseDir, "TestRepo.sqlite")))
                {
                    var repoName = "TestRepo";
                    var encryptionPassword = "correct-horse-battery-staple";
                    var repo = new Repository(repoName, storage, encryptionPassword);

                    repo.Initialize().Status.ShouldBe(InitRepositoryStatus.Success);

                    var vfsName = "TestVfs";
                    var vfs = new VirtualFilesystem(repo, vfsName);

                    vfs.Sync(Path.Join(contentDir), "vfs://some/dir");

                    vfs.ListDirectoryRecursive(new DirectoryPath("/"));

                    vfs.Sync("vfs://some/dir", downloadDir);

                    File.Exists(Path.Join(downloadDir, "a.txt")).ShouldBeTrue();
                    Directory.Exists(Path.Join(downloadDir, "b")).ShouldBeTrue();
                    File.Exists(Path.Join(downloadDir, "b/c.txt")).ShouldBeTrue();
                    File.Exists(Path.Join(downloadDir, "b/d.txt")).ShouldBeTrue();
                    Directory.Exists(Path.Join(downloadDir, "b/e")).ShouldBeTrue();
                    File.Exists(Path.Join(downloadDir, "b/e/f.txt")).ShouldBeTrue();
                }
            }
        }

        // Syncing a file in a directory should cause that directory's version to increase, but all parents
        // of that directory should be unchanged.
        [Fact]
        public void UploadedVersionNumbersShouldBeCorrect()
        {
            using (var autoDeleteDirectory = new AutoDeleteDirectory())
            {
                var configDir = Path.Join(autoDeleteDirectory.Path, "config"); ;
                var baseDir = Path.Join(autoDeleteDirectory.Path, "repo");
                var contentDir = Path.Join(autoDeleteDirectory.Path, "content");
                var downloadDir = Path.Join(autoDeleteDirectory.Path, "download");

                Utils.CreateDirectoryTree(contentDir, "a.txt", "b/c.txt", "b/d.txt", "b/e/f.txt");

                using (var storage = new SqliteStorage(Path.Join(baseDir, "TestRepo.sqlite")))
                {
                    var repoName = "TestRepo";
                    var encryptionPassword = "correct-horse-battery-staple";
                    var repo = new Repository(repoName, storage, encryptionPassword);

                    repo.Initialize().Status.ShouldBe(InitRepositoryStatus.Success);

                    var vfsName = "TestVfs";
                    var vfs = new VirtualFilesystem(repo, vfsName);

                    vfs.Sync(Path.Join(contentDir), "vfs://some/dir");
                    var (metadata, version) = vfs.GetDirectory("vfs://some", new Timestamp(DateTime.MaxValue));
                    version.ShouldBe(0);
                    metadata!.Path.ToString().ShouldBe("some");


                    vfs.Sync(Path.Join(contentDir), "vfs://some/dir2");
                    
                    (metadata, version) = vfs.GetDirectory("vfs://", new Timestamp(DateTime.MaxValue));
                    version.ShouldBe(0);
                    metadata!.Path.ToString().ShouldBe("");

                    (metadata, version) = vfs.GetDirectory("vfs://some", new Timestamp(DateTime.MaxValue));
                    version.ShouldBe(1);
                    metadata!.Path.ToString().ShouldBe("some");

                    vfs.ListDirectoryRecursive(new DirectoryPath("/"));
                }
            }
        }
    }
}
