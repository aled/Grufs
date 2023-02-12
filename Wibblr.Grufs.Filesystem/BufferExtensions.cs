using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Filesystem
{
    public static class BufferExtensions
    {
        public static VirtualDirectory ReadVirtualDirectory(this BufferReader reader)
        {
            return new VirtualDirectory(reader);
        }

        public static BufferBuilder AppendVirtualDirectory(this BufferBuilder builder, VirtualDirectory directory) 
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

        public static FileMetadata ReadFileMetadata(this BufferReader reader)
        {
            return new FileMetadata(reader);
        }

        public static BufferBuilder AppendFileMetadata(this BufferBuilder builder, FileMetadata file)
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
