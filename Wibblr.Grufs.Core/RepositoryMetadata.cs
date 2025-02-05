using System.Buffers.Binary;

using Wibblr.Grufs.Encryption;

namespace Wibblr.Grufs.Core
{
    internal class RepositoryMetadata
    {
        public byte SerializationVersion { get; init; } = 0;
        public InitializationVector MasterKeysInitializationVector { get; init; }
        public Salt Salt { get; init; }
        public int Iterations { get; init; }
        public byte[] EncryptedMasterKeys { get; init; }

        public RepositoryMetadata(InitializationVector masterKeysInitializationVector, Salt salt, int iterations, ReadOnlySpan<byte> encryptedMasterKeys)
        {
            MasterKeysInitializationVector = masterKeysInitializationVector;
            Salt = salt;
            Iterations = iterations;
            EncryptedMasterKeys = encryptedMasterKeys.ToArray();
        }

        public RepositoryMetadata(ArrayBuffer buffer)
        {
            try
            {
                var reader = new BufferReader(buffer);
                SerializationVersion = reader.ReadByte();
                MasterKeysInitializationVector = reader.ReadInitializationVector();
                Salt = reader.ReadSalt();
                Iterations = reader.ReadInt();
                EncryptedMasterKeys = reader.ReadSpan().ToArray();
            }
            catch (Exception) 
            {
                throw new Exception("Invalid repository metadata");
            }
        }

        public ArrayBuffer Serialize()
        {
            if (EncryptedMasterKeys.Length > 255)
            {
                throw new ArgumentException("Invalid encrypted master keys length");
            }

            var bufferLength =
                1 + // serializationVersion
                InitializationVector.Length +
                Salt.Length +
                Iterations.GetSerializedLength() +
                EncryptedMasterKeys.GetSerializedLength();

            var buffer = new BufferBuilder(bufferLength)
                .AppendByte(SerializationVersion)
                .AppendInitializationVector(MasterKeysInitializationVector)
                .AppendSalt(Salt)
                .AppendInt(Iterations)
                .AppendSpan(EncryptedMasterKeys)
                .ToBuffer();

            return buffer;
        }
    }
}
