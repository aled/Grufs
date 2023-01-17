using System;

namespace Wibblr.Grufs
{
    public interface IChunkSource
    {
        public bool Available();

        public bool IsCompleted();

        public (byte[], long, int) Next();
    }
}
