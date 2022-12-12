using System.Buffers.Binary;
using System.Numerics;
using System.Text;

using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs
{
    internal class RepositoryMetadata
    {
        public byte SerializationVersion { get; init; } = 0;
        public InitializationVector MasterKeysInitializationVector { get; init; }
        public Salt Salt { get; init; }
        public int Iterations { get; init; }
        public byte[] EncryptedMasterKeys { get; init; }

        public byte[] Serialize()
        {
            if (EncryptedMasterKeys.Length > 255)
            {
                throw new ArgumentException("Invalid encrypted master keys length");
            }

            var buffer = new Buffer(1 + InitializationVector.Length + Salt.Length + sizeof(int) + 1 + EncryptedMasterKeys.Length)
                .Append(SerializationVersion)
                .Append(MasterKeysInitializationVector.ToSpan())
                .Append(Salt.ToSpan())
                .Append(Iterations)
                .Append((byte)EncryptedMasterKeys.Length)
                .Append(EncryptedMasterKeys);

            return buffer.Bytes;
        }

        public RepositoryMetadata(InitializationVector masterKeysInitializationVector, Salt salt, int iterations, ReadOnlySpan<byte> encryptedMasterKeys)
        {
            MasterKeysInitializationVector= masterKeysInitializationVector;
            Salt= salt;
            Iterations= iterations;
            EncryptedMasterKeys = encryptedMasterKeys.ToArray();
        }

        public RepositoryMetadata(ReadOnlySpan<byte> serialized)
        {
            try
            {
                int pos = 0;
                SerializationVersion = serialized[0]; 
                pos += 1;

                MasterKeysInitializationVector = new InitializationVector(serialized.Slice(pos, InitializationVector.Length)); 
                pos += InitializationVector.Length;

                Salt = new Salt(serialized.Slice(pos, Salt.Length));
                pos += Salt.Length;

                Iterations = BinaryPrimitives.ReadInt32BigEndian(serialized.Slice(pos, 4));
                pos += 4;

                var EncryptedMasterKeysLength = serialized[pos];
                pos += 1;

                EncryptedMasterKeys = serialized.Slice(pos, EncryptedMasterKeysLength).ToArray();
                pos += EncryptedMasterKeysLength;
            }
            catch(Exception) 
            {
                throw new Exception("Invalid repository metadata");
            }
        }
    }
}
