﻿using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Tests
{
    public class TemporaryLocalStorage : IChunkStorageFactory, IFileStorageFactory, IDisposable
    {
        internal AbstractFileStorage _storage;
        internal AutoDeleteDirectory _autoDeleteDirectory;

        public IChunkStorage GetChunkStorage() => _storage;
        public AbstractFileStorage GetFileStorage() => _storage;

        public TemporaryLocalStorage()
        {
            _autoDeleteDirectory = new AutoDeleteDirectory();
            Log.WriteLine(0, $"Using local temporary directory {_autoDeleteDirectory.Path}");

            _storage = new LocalStorage(_autoDeleteDirectory.Path);
            _storage.CreateDirectory("", createParents: true);
        }

        public void Dispose()
        {
            _autoDeleteDirectory.Dispose();
        }
    }
}
