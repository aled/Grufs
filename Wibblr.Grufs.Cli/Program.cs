﻿using Wibblr.Grufs.Logging;

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
    ///   - VFS      Individual files or directories can be synced (uploaded and downloaded) to a virtual filesystem on the remote storage. Multiple clients can use the same 
    ///              virtual filesystem. Importantly, files are deduplicated regardless of whether they are backed up or synced.
    ///              It is possible to retrieve all previous versions of a synced file or directory.
    ///
    /// Usage:
    ///   - grufs.exe command subcommand [options]
    ///       where command is repo, vfs, backup, restore
    ///
    ///  - repo subcommands:
    ///      init, register, unregister, list, scrub
    ///
    ///      - grufs.exe repo init --name myrepo [--username user] [--password password] [--identity mykey.rsa] [--encryption-password password] sftp://myhost.com/grufs-storage/repo1
    ///      - grufs.exe repo init --name myrepo --encryption-password password sqlite://c:\temp\myrepo.sqlite
    ///      - grufs.exe repo init --name myrepo ~/grufs-storage/repo1   # The '~' character is expanded to the user's home directory, including on Windows.
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
    ///       - grufs.exe repo list --verbose
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
    ///  - VFS subcommands
    ///    -- grufs.exe vfs sync -r -n myrepo c:\mydirectory vfs://some/other/directory
    ///
    /// </summary>
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                Log.StdOutIsConsole = true;
                Environment.Exit(await new Program().RunAsync(args, CancellationToken.None));
            }
            catch (RepositoryException re)
            {
                Log.WriteLine(0, re.Message);
                Environment.Exit(-2);
            }
            catch (UsageException ue)
            {
                Console.WriteLine("Usage: grufs command subcommand options");
                Environment.Exit(-1);
            }
        }

        public async Task<int> RunAsync(string[] args, CancellationToken token)
        {
            return args switch
            {
                ["repo", .. var remainingArgs] => await new RepoMain().RunAsync(remainingArgs, token),
                ["backup", .. var remainingArgs] => throw new NotImplementedException(),
                ["restore", .. var remainingArgs] => throw new NotImplementedException(),
                ["vfs", .. var remainingArgs] => await new VfsMain().RunAsync(remainingArgs, token),
                _ => throw new UsageException("No subcommand specified; must be one of 'repo', 'backup', 'restore', 'vfs'")
            };
            
        }
    }
}