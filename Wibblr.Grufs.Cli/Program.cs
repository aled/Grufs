namespace Wibblr.Grufs.Cli
{
    /// <summary>
    /// Grand Remote Unified File System
    ///
    /// Stores encrypted and deduplicated data on a local or remote disk. The data cannot be decrypted by the server. Currently only SFTP is implemented.
    /// Designed to allow the following scenarios:
    ///
    ///   - Backup   Can be used to securely store backups of a local filesystem. These backups are immutable.
    ///              Works similarly to restic/tarsnap/borg, but usable on Windows.
    ///   
    ///   - Vfs      Individual files or directories can be synced (uploaded and downloaded) to a virtual filesystem on the remote storage. Multiple clients can use the same 
    ///              virtual filesystem. Importantly, files are deduplicated regardless of whether they are backed up or synced.
    ///              It is possible to retrieve all previous versions of a synced file or directory.
    ///
    /// Usage:
    ///   - grufs.exe command subcommand [options]
    ///       where command is repo, backup, restore, vfs
    ///
    ///  - repo subcommands:
    ///      init, register, unregister, list, scrub
    ///
    ///      - grufs.exe repo --init --name myrepo --protocol sftp --host hostname --port port --user user --storage-password password --identity mykey.rsa --basedir ~/grufs-storage/repo1 --encryption-password password
    ///      - grufs.exe repo --init --name myrepo --protocol sqlite --basedir ~/grufs-storage --encryption-password password
    ///      - grufs.exe repo --init --name myrepo --basedir ~/grufs-storage/repo1   # The '~' character is expanded to the user's home directory, including on Windows.
    ///   
    ///      This will:
    ///        - create the directory on the (possibly remote) server
    ///        - login to sftp if using public key authentication
    ///        - prompt for ssh password if required
    ///        - prompt for storage password if required
    ///        - create the metadata for the default repository
    ///        - show error if metadata already exists and password is incorrect
    ///        - update local config with 'mystorage = sftp://user@hostname:port/grufs-storage'
    ///
    ///       - grufs.exe repo --list --verbose
    /// 
    ///       This will:
    ///         - list all repositories previously created
    ///         - the verbose flag will include: creation date, list of backupsets.
    ///
    ///       - grufs.exe repo scrub
    ///
    ///       This will:
    ///       - download all information from storage
    ///
    ///  - backup/restore subcommands
    ///    -- grufs.exe backup mybackup --storage mystorage  --directory 'c:\my documents' 
    ///    -- grufs.exe backup mybackup
    ///    -- grufs.exe restore mybackup --include **/*.mp3 --destination c:\my-restore-dir 
    ///
    ///  - vfs subcommands
    ///    -- grufs.exe vfs [--list-versions] [--upload] [--download] [--no-recursive] [--no-delete] [--trust-storage] c:\mydirectory my-storage:my-virtual-filesystem:some/other/directory
    ///
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            Environment.Exit(new Program().Run(args));
        }

        public int Run(string[] args)
        {
            try
            {
                return args switch
                {
                    ["repo", .. var remainingArgs] => new RepoMain().Run(remainingArgs),
                    ["backup", .. var remainingArgs] => throw new NotImplementedException(),
                    ["restore", .. var remainingArgs] => throw new NotImplementedException(),
                    ["vfs", .. var remainingArgs] => new VfsMain().Run(remainingArgs),
                    _ => throw new UsageException("No subcommand specified; must be one of 'repo', 'backup', 'restore', 'vfs'")
                };
            }
            catch (UsageException ue)
            {
                Console.WriteLine(ue.Message);
                return -1;
            }
        }
    }
}