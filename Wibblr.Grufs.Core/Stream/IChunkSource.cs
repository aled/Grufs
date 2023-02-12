using System;

namespace Wibblr.Grufs.Core
{
    public interface IChunkSource
    {
        public bool Available();

        public bool IsCompleted();

        public (byte[], long, int) Next();
    }
}
