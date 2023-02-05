using System.Security.Cryptography;

using FluentAssertions;

namespace Wibblr.Grufs.Tests
{
    public class CliTests
    {
        [Fact]
        public void RepoInitShouldCreateRegistration()
        {
            var uniquifier = $"{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";
            var configDir = Path.Join(Path.GetTempPath(), "grufs", $"config-{uniquifier}");
            var baseDir = Path.Join(Path.GetTempPath(), "grufs", $"test-{uniquifier}");
            var repoName = "TestRepo";
            var encryptionPassword = "correct-horse-battery-staple";

            new Cli.Program().Run(new[] { "repo", "--init", "--config-dir", configDir, "--non-interactive", "--name", repoName, "--basedir", baseDir, "--encryption-password", encryptionPassword });

            Directory.Exists(baseDir).Should().BeTrue();

            // storage should be registered in config
            var repoRegistration = File.ReadAllText(Path.Join(configDir, "repos", repoName));
            repoRegistration.Should().Contain($"repoName:{repoName}");
            repoRegistration.Should().Contain($"baseDir:{baseDir}");
            repoRegistration.Should().Contain($"encryptionPassword:{encryptionPassword}");
            repoRegistration.Should().Contain($"repoName:{repoName}");

            Directory.Delete(baseDir, true);
            Directory.Delete(configDir, true);
        }

        [Fact]
        public void SyncDirectory()
        {
            var uniquifier = $"{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";
            var tempPath = Path.Join(Path.GetTempPath(), $"grufs-{uniquifier}");
            var configDir = Path.Join(tempPath, "config"); ;
            var baseDir = Path.Join(tempPath, "repo");
            var contentDir = Path.Join(tempPath, "content");

            var repoName = "TestRepo";
            var encryptionPassword = "correct-horse-battery-staple";

            new Cli.Program().Run(new[] { "repo", "--init", "--config-dir", configDir, "--non-interactive", "--name", repoName, "--basedir", baseDir, "--encryption-password", encryptionPassword });

            // create files:
            // a.txt
            // b/c.txt
            // b/d.txt
            // b/e/f.txt

            Directory.CreateDirectory(contentDir);
            File.WriteAllText(Path.Combine(contentDir, "a.txt"), "hello a");
            Directory.CreateDirectory(Path.Combine(contentDir, "b"));
            File.WriteAllText(Path.Combine(contentDir, "b", "c.txt"), "hello c");
            File.WriteAllText(Path.Combine(contentDir, "b", "d.txt"), "hello d");
            Directory.CreateDirectory(Path.Combine(contentDir, "b", "e"));
            File.WriteAllText(Path.Combine(contentDir, "b", "e", "f.txt"), "hello f");

            new Cli.Program().Run(new[] { "sync", "-r", "--config-dir", configDir, "--repo", repoName, "--upload", "--local", contentDir, "--virtual", "some/subdir"  });

            var downloadDir = Path.Join(tempPath, "download");

            new Cli.Program().Run(new[] { "sync", "-r", "--config-dir", configDir, "--repo", repoName, "--download", "--local", downloadDir, "--virtual", "/" });

            File.ReadAllText(Path.Combine(downloadDir, "some", "subdir", "a.txt")).Should().Be("hello a");
            File.ReadAllText(Path.Combine(downloadDir, "some", "subdir", "b", "c.txt")).Should().Be("hello c");
            File.ReadAllText(Path.Combine(downloadDir, "some", "subdir", "b", "d.txt")).Should().Be("hello d");
            File.ReadAllText(Path.Combine(downloadDir, "some", "subdir", "b", "e", "f.txt")).Should().Be("hello f");
        }
    }
}
