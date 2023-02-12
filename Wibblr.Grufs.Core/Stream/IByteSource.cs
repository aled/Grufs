using System;

namespace Wibblr.Grufs.Core
{
    public interface IByteSource
    {
        bool Available();

        bool IsCompleted();

        byte Next();
    }
}
