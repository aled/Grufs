using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wibblr.Grufs.Cli.Sync
{
    public class SyncConfig
    {
        public string? ConfigDir; 
        public string? RepoName;
        public bool Upload;
        public bool Download;
        public bool Delete;
        public bool Recursive;
        public string? LocalPath;
        public string? VirtualPath;
        public bool FileOnly;
    }
}
