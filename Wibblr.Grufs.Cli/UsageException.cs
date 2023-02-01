using System.Runtime.Serialization;

namespace Wibblr.Grufs.Cli
{
    [Serializable]
    internal class UsageException : Exception
    {
        public UsageException()
        {
        }
    }
}