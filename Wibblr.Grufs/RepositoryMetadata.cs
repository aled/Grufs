using System.Buffers.Binary;

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

        public ReadOnlySpan<byte> Serialize()
        {
            if (EncryptedMasterKeys.Length > 255)
            {
                throw new ArgumentException("Invalid encrypted master keys length");
            }

            var buffer = new BufferBuilder(1 + InitializationVector.Length + Salt.Length + sizeof(int) + 1 + EncryptedMasterKeys.Length)
                .AppendByte(SerializationVersion)
                .AppendBytes(MasterKeysInitializationVector.ToSpan())
                .AppendBytes(Salt.ToSpan())
                .AppendInt(Iterations)
                .AppendByte((byte)EncryptedMasterKeys.Length)
                .AppendBytes(EncryptedMasterKeys)
                .ToBuffer();

            return buffer.AsSpan();
        }

        public RepositoryMetadata(InitializationVector masterKeysInitializationVector, Salt salt, int iterations, ReadOnlySpan<byte> encryptedMasterKeys)
        {
            MasterKeysInitializationVector = masterKeysInitializationVector;
            Salt = salt;
            Iterations = iterations;
            EncryptedMasterKeys = encryptedMasterKeys.ToArray();
        }

        public RepositoryMetadata(Buffer buffer)
        {
            try
            {
                var reader = new BufferReader(buffer);
                SerializationVersion = reader.ReadByte();
                MasterKeysInitializationVector = new InitializationVector(reader.ReadBytes(InitializationVector.Length));
                Salt = new Salt(reader.ReadBytes(Salt.Length));
                Iterations = reader.ReadInt();
                var EncryptedMasterKeysLength = reader.ReadByte();
                EncryptedMasterKeys = reader.ReadBytes(EncryptedMasterKeysLength).ToArray();
            }
            catch (Exception) 
            {
                throw new Exception("Invalid repository metadata");
            }
        }
    }
}
