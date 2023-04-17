using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Filesystem
{
    public static class BufferExtensions
    {
        public static VfsDirectoryMetadata ReadVirtualDirectory(this BufferReader reader)
        {
            return new VfsDirectoryMetadata(reader);
        }

        public static BufferBuilder AppendVirtualDirectory(this BufferBuilder builder, VfsDirectoryMetadata directory) 
        { 
            directory.SerializeTo(builder);
            return builder;
        }

        public static DirectoryPath ReadDirectoryPath(this BufferReader reader)
        {
            return new DirectoryPath(reader);
        }

        public static BufferBuilder AppendDirectoryPath(this BufferBuilder builder, DirectoryPath path)
        {
            path.SerializeTo(builder);
            return builder;
        }

        public static VfsFileMetadata ReadFileMetadata(this BufferReader reader)
        {
            return new VfsFileMetadata(reader);
        }

        public static BufferBuilder AppendFileMetadata(this BufferBuilder builder, VfsFileMetadata file)
        {
            file.SerializeTo(builder);
            return builder;
        }

        public static Filename ReadFilename(this BufferReader reader)
        {
            return new Filename(reader);
        }

        public static BufferBuilder AppendFilename(this BufferBuilder builder, Filename filename)
        {
            filename.SerializeTo(builder);
            return builder;
        }
    }
}
