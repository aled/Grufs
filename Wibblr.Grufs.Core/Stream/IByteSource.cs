using System;

namespace Wibblr.Grufs
{
    public interface IByteSource
    {
        bool Available();

        bool IsCompleted();

        byte Next();
    }
}
