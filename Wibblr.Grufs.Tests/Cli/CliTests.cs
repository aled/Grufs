using Wibblr.Grufs.Cli;
using Wibblr.Grufs.Filesystem;

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
            using (var autoDeleteDirectory = new AutoDeleteDirectory())
            {
                var configDir = Path.Join(autoDeleteDirectory.Path, "config");
                var baseDir = Path.Join(autoDeleteDirectory.Path, "repo");
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
            }
        }

        // TODO: enter passwords interactively if not set
        // TODO: error if passwords not set and non-interactive is set

        [Fact]
        public void SyncDirectory()
        {
            using (var autoDeleteDirectory = new AutoDeleteDirectory())
            {
                var configDir = Path.Join(autoDeleteDirectory.Path, "config"); ;
                var baseDir = Path.Join(autoDeleteDirectory.Path, "repo");
                var contentDir = Path.Join(autoDeleteDirectory.Path, "content");

                var repoName = "TestRepo";
                var encryptionPassword = "correct-horse-battery-staple";

                new Cli.Program().Run(new[] { "repo", "init", "--config-dir", configDir, "--name", repoName, "--basedir", baseDir, "--encryption-password", encryptionPassword });

                Utils.CreateDirectoryTree(contentDir, "a.txt", "b/c.txt", "b/d.txt", "b/e/f.txt");

                new Cli.Program().Run(new[] { "vfs", "sync", "-rc", configDir, "--repo-name", repoName, contentDir, "vfs://some/subdir" });

                Log.WriteLine(0, "Listing repo");

                new Cli.Program().Run(new[] { "vfs", "ls", "-rc", configDir, "--repo-name", repoName, "vfs://" });

                var downloadDir = Path.Join(autoDeleteDirectory.Path, "download");

                //new Cli.Program().Run(new[] { "vfs", "sync", "-r", "--config-dir", configDir, "-n", repoName, "vfs://", downloadDir });

                //File.ReadAllText(Path.Combine(downloadDir, "some", "subdir", "a.txt")).Should().Be(Utils.GetFileContent("a.txt"));
                //File.ReadAllText(Path.Combine(downloadDir, "some", "subdir", "b", "c.txt")).Should().Be(Utils.GetFileContent("b/c.txt"));
                //File.ReadAllText(Path.Combine(downloadDir, "some", "subdir", "b", "d.txt")).Should().Be(Utils.GetFileContent("b/d.txt"));
                //File.ReadAllText(Path.Combine(downloadDir, "some", "subdir", "b", "e", "f.txt")).Should().Be(Utils.GetFileContent("b/e/f.txt"));
            }
        }
    }
}
