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

        public int CiphertextLength(int plaintextLength) => aes.GetCiphertextLengthCbc(plaintextLength);

        public void Encrypt(ReadOnlySpan<byte> plaintext, InitializationVector iv, EncryptionKey key, Span<byte> destination)
        {
            if (destination.Length != CiphertextLength(plaintext.Length))
            {
                throw new Exception("Invalid destination length");
            }

            aes.Key = key.Value;
            if (!aes.TryEncryptCbc(plaintext, iv.Value, destination, out _))
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
        public int MaxPlaintextLength(int ciphertextLength) => ciphertextLength - 1;

        public int Decrypt(ReadOnlySpan<byte> ciphertext, InitializationVector iv, EncryptionKey key, Span<byte> destination)
        {
            aes.Key = key.Value;
            if (!aes.TryDecryptCbc(ciphertext, iv.Value, destination, out int bytesWritten))
            {
                throw new Exception("Failed to decrypt");
            }

            return bytesWritten;
        }
    }
}
