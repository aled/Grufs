using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wibblr.Grufs.Cli
{
    public class VfsArgs
    {
        public enum OperationEnum
        {
            None,
            List,
            Sync
        }

        public string? ConfigDir; 
        public string? RepoName;
        public OperationEnum Operation;
        public bool Delete;
        public bool Recursive;
        public bool Progress;
        public int Verbose;
        public string? Source;
        public string? Destination;
        public bool FileOnly;
    }
}
