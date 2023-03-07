using System.Security.Cryptography;

using Wibblr.Grufs.Cli;

namespace Wibblr.Grufs.Tests
{
    public class CliTests
    {
        [Fact]
        public void ArgParserShouldDetectDuplicateDefinitions()
        {
            List<string> l = new List<string>();
            Action<string> action = s => l.Append(s);

            var definitions = new ArgDefinition[]
            {
                new NamedArgDefinition('a', "asdf", action),
                new NamedArgDefinition('a', "qwer", action),
                new NamedArgDefinition('b', "qwer", action),
                new NamedArgDefinition('c', "zxcv", action),
                new NamedArgDefinition('d', "zxcv", action),
            };

            new Action(() => new ArgParser(definitions)).Should().Throw<Exception>().WithMessage("Duplicate argument definition(s): 'a',' qwer',' zxcv'");
        }

        [Fact]
        public void RepoInitShouldCreateRegistration()
        {
            var uniquifier = $"{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}";
            var configDir = Path.Join(Path.GetTempPath(), "grufs", $"config-{uniquifier}");
            var baseDir = Path.Join(Path.GetTempPath(), "grufs", $"test-{uniquifier}");
            var repoName = "TestRepo";
            var encryptionPassword = "correct-horse-battery-staple";

            new Program().Run(new[] { "repo", "init", "--config-dir", configDir, "--non-interactive", "--name", repoName, "--basedir", baseDir, "--encryption-password", encryptionPassword });

            Directory.Exists(baseDir).Should().BeTrue();

            // storage should be registered in config
            var repoRegistration = File.ReadAllText(Path.Join(configDir, "repos", repoName));
            repoRegistration.Should().Contain($"repoName:\"{repoName}\"");
            repoRegistration.Should().Contain($"baseDir:\"{baseDir.Replace("\\", "\\\\")}\"");
            repoRegistration.Should().Contain($"encryptionPassword:\"{encryptionPassword}\"");
            repoRegistration.Should().Contain($"repoName:\"{repoName}\"");

            Directory.Delete(baseDir, true);
            Directory.Delete(configDir, true);
        }

        // TODO: enter passwords interactively if not set
        // TODO: error if passwords not set and non-interactive is set

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

            new Cli.Program().Run(new[] { "repo", "init", "--config-dir", configDir, "--name", repoName, "--basedir", baseDir, "--encryption-password", encryptionPassword });

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

            new Cli.Program().Run(new[] { "vfs", "sync", "-rc", configDir, "--repo-name", repoName, contentDir, "vfs://some/subdir" });

            // TODO: list virtual files

            var downloadDir = Path.Join(tempPath, "download");

            new Cli.Program().Run(new[] { "vfs", "sync", "-r", "--config-dir", configDir, "-n", repoName, "vfs://", downloadDir });

            File.ReadAllText(Path.Combine(downloadDir, "some", "subdir", "a.txt")).Should().Be("hello a");
            File.ReadAllText(Path.Combine(downloadDir, "some", "subdir", "b", "c.txt")).Should().Be("hello c");
            File.ReadAllText(Path.Combine(downloadDir, "some", "subdir", "b", "d.txt")).Should().Be("hello d");
            File.ReadAllText(Path.Combine(downloadDir, "some", "subdir", "b", "e", "f.txt")).Should().Be("hello f");
        }
    }
}
