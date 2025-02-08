namespace Wibblr.Grufs.Cli
{
    public class RepoArgs
    {
        public enum OperationEnum
        {
            None,
            Init, 
            Register, 
            Unregister, 
            List, 
            Scrub
        }

        public bool NonInteractive = false;
        public string? ConfigDir;

        public OperationEnum Operation = OperationEnum.None;

        public string? RepoName;
        public string? Username;
        public string? Password;
        public string? EncryptionPassword;
        public string? Location;
    }
}