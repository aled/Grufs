using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs
{
    /// <summary>
    /// Directories are versioned by storing every change to the directory in a a series of encrypted files
    /// 
    /// The address of the encrypted file is the name of the directory, plus a 128-bit sequence number
    /// 
    /// The contents of each encrypted file includes the change(s) made to the directory, plus the address and hashed content of 
    /// previous versions along with their timestamps (n-1 -> 2022-01-01 14:23, n-10 -> 2021-01-01 13:24, n-100, n-1000),
    /// formaing a blockchain of sorts. This enables a client to find earlier versions at any point in time using a binary-like search.
    /// 
    /// Additional mechanisms for caching state and/or quickly computing state may be added later.
    /// </summary>
    
    public enum Operation
    {
        Create,
        Update,
        Delete
    }

    public class OperationGroup
    {
        // the timestamp of the changeset is the latest timestamp of all included changes.
        public long SequenceNumber;
        public DateTime timestamp;
        public Address previousChangesetAddress;
        public ChunkType previousChangesetChunkType;
        public DateTime previousChangesetTimestamp;

        public IList<DirectoryOperation> DirectoryOperations = new List<DirectoryOperation>();
        public IList<FileOperation> FileOperations = new List<FileOperation>();
    }

    public class DirectoryOperation
    {
        Operation operation;
        string name;
        DateTime timestamp;
    }

    public class FileOperation
    {
        Operation operation;
        string name;
        byte[] address;
        byte chunkType;
        long size;
        DateTime timestamp;
    }

    public class VersionedDirectory
    {

    }

    public class VersionedFile
    {

    }

    public class VersionedFilesystem
    {
        private IChunkRepository _repository;

        private VersionedFilesystem(IChunkRepository repository)
        {
            _repository = repository;
        }

        public VersionedDirectory CreateDirectory()
        {
            throw new NotImplementedException();
        }
    }
}
