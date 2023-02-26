﻿namespace Wibblr.Grufs.Tests
{
    // Filesystem converts stream of updates into hierarchical filesystem

    // Filesystem storage converts stream of updates into encrypted chunks


    // encrypt directory

    // encrypt filename

    // modify directory metadata

    // modify file metadata

    // move file to different directory

    // delete file

    // show history

    // restore deleted file

    // make historic snapshot available

    record DirectoryUpdate
    {
        public string name;
        public DateTime timestamp;
        bool isDeleted;

        public DirectoryUpdate(string name, DateTime timestamp, bool isDeleted = false)
        {
            this.name = name;
            this.timestamp = timestamp;
            this.isDeleted = isDeleted;
        }
    }

    record FileUpdate
    {
        public string name;
        public DateTime timestamp;
        public int size;
        public byte[] content;
        bool isDeleted;

        public FileUpdate(string name, DateTime timestamp, int size, byte[] content, bool isDeleted = false)
        {
            this.name = name;
            this.timestamp = timestamp;
            this.size = size;
            this.content = content;
            this.isDeleted = isDeleted;
        }
    }

    public class VirtualFilesystemTests
    {
        [Fact]
        public void AddFileToRepository()
        {
            // start with an empty filesystem
            // add root directory
            var repository = new InMemoryChunkStorage();


            //var versionedFilesystem = new VersionedFilesystem(repository);





            new DirectoryUpdate("/", DateTime.Parse("2022-01-01T13:00:00"));

            


            //     subdirectory
            //     file

            // modify file
            //   in root
            //   in subdir

            // modify directory:
            //   create file
            //   delete file


            var expectedFilesystem = """
                /            { "timestamp": "2022-01-01T13:00:00" }
                |- a.txt     { "timestamp": "2022-01-01T13:00:00", "size": "123", "content": "b123456" }
                |- b.txt     { "timestamp": "2022-01-01T13:00:00", "size": "123", "tree": "c234567" }
                |- c         { "timestamp": "2022-01-01T14:00:00" }
                   |- d.txt
                """;
        }
    }
}