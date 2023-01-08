using System;
using System.Security.Cryptography;

namespace Wibblr.Grufs.Encryption
{
    /// <summary>
    /// Thin wrapper around all cryptographic operations. Encapsulates the algorithm used and all encryption parameters.
    /// </summary>
    public class Encryptor
    {
        private Aes aes = Aes.Create();

        public Encryptor()
        {
            aes.Padding = PaddingMode.PKCS7;
            aes.KeySize = EncryptionKey.Length * 8;
        }

        public int CiphertextLength(int plaintextLength) => aes.GetCiphertextLengthCbc(plaintextLength + Checksum.Length);

        public byte[] Encrypt(ReadOnlySpan<byte> plaintext, InitializationVector iv, EncryptionKey key)
        {
            var ciphertext = new byte[CiphertextLength(plaintext.Length)];
            Encrypt(plaintext, iv, key, ciphertext);
            return ciphertext;
        }

        public void Encrypt(ReadOnlySpan<byte> plaintext, InitializationVector iv, EncryptionKey key, Span<byte> destination)
        {
            if (destination.Length != CiphertextLength(plaintext.Length))
            {
                throw new Exception("Invalid destination length");
            }

            aes.Key = key.Value;

            var checksum = Checksum.Build(plaintext);
            var source = new byte[plaintext.Length + Checksum.Length];
            plaintext.CopyTo(source);
            checksum.ToSpan().CopyTo(source.AsSpan(plaintext.Length));

            if (!aes.TryEncryptCbc(source, iv.Value, destination, out _))
            {
                throw new Exception("Error during encryption");
            }
        }

        /// <summary>
        /// It is not possible to know the exact plaintext length until the decryption is complete.
        /// However using PKCS7 padding, the upper bound on the plaintext length is the ciphertext length - 1
        /// </summary>
        /// <param name="ciphertextLength"></param>
        /// <returns></returns>
        private int MaxPlaintextLength(int ciphertextLength) => ciphertextLength - 1 - Checksum.Length;

        public (byte[], int) Decrypt(ReadOnlySpan<byte> ciphertext, InitializationVector iv, EncryptionKey key)
        {
            var bytes = new byte[MaxPlaintextLength(ciphertext.Length)];
            var bytesWritten = Decrypt(ciphertext, iv, key, bytes);
            return (bytes, bytesWritten);
        }

        private int Decrypt(ReadOnlySpan<byte> ciphertext, InitializationVector iv, EncryptionKey key, Span<byte> destination)
        {
            var temp = new byte[MaxPlaintextLength(ciphertext.Length + Checksum.Length)];

            aes.Key = key.Value;
            if (!aes.TryDecryptCbc(ciphertext, iv.Value, temp, out int bytesWritten))
            {
                throw new Exception("Failed to decrypt");
            }

            var calculatedChecksum = Checksum.Build(temp.AsSpan(0, bytesWritten - Checksum.Length));
            var checksum = new Checksum(temp.AsSpan(bytesWritten - Checksum.Length, Checksum.Length));

            if (checksum != calculatedChecksum)
            {
                throw new Exception("Failed to decrypt - invalid encrypted checksum");
            }

            temp.AsSpan(0, bytesWritten - Checksum.Length).CopyTo(destination);

            return bytesWritten - Checksum.Length;
        }
    }
}
