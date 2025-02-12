using System;

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
        public bool Recursive = true;
        public bool Progress;
        public int Verbose;
        public bool Human;
        public string? Source;
        public string? Destination;
    }
}
