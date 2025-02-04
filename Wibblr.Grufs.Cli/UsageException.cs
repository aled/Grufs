using System.Runtime.Serialization;

namespace Wibblr.Grufs.Cli
{
    [Serializable]
    internal class UsageException : Exception
    {
        public UsageException()
        {
        }

        public UsageException(string? message) : base(message)
        {
        }

        public UsageException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}