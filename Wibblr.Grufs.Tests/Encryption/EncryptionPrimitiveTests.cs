using Wibblr.Grufs.Encryption;
using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Tests
{
    public class EncryptionPrimitiveTests
    {
        // wrap key
        // unwrap key
        [Fact]
        public void CheckLengthOfWrappedKeys()
        {
            // Null array of bytes
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
            new Action(() => new WrappedEncryptionKey(null)).Should().ThrowExactly<ArgumentException>();
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.

            new Action(() => new WrappedEncryptionKey(new[] { (byte)0 })).Should().ThrowExactly<ArgumentException>();
        }

        [Fact]
        public void WrapKey()
        {
            // Test data copied from https://www.rfc-editor.org/rfc/rfc3394#section-4
            var kek = new KeyEncryptionKey("000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F".ToBytes());
            var key = new EncryptionKey("00112233445566778899AABBCCDDEEFF000102030405060708090A0B0C0D0E0F".ToBytes());
            var expectedWrappedKey = new WrappedEncryptionKey("28C9F404C4B810F4CBCCB35CFB87F8263F5786E2D80ED326CBC7F0E71A99F43BFB988B9B7A02DD21".ToBytes());

            var wrappedKey = key.Wrap(kek);
            wrappedKey.ToString().Should().BeEquivalentTo(expectedWrappedKey.ToString());

            var unwrappedKey = wrappedKey.Unwrap(kek);
            unwrappedKey.ToString().Should().BeEquivalentTo(key.ToString());
        }

        [Fact]
        public void AddressMustBeInitialized()
        {
            new Action(() => new Address()).Should().ThrowExactly<NotImplementedException>();
        }

        [Fact]
        public void AddressShouldBe32Bytes()
        {
            new Action(() => new Address(new byte[31])).Should().ThrowExactly<ArgumentException>();
        }

        [Fact]
        public void AddressShouldNotBeNull()
        {
            new Action(() => new Address(null)).Should().ThrowExactly<ArgumentException>();
        }

        [Fact]
        public void EncryptionKeyShouldBe32Bytes()
        {
            new Action(() => new EncryptionKey(new byte[31])).Should().ThrowExactly<ArgumentException>();
        }

        [Fact]
        public void EncryptionKeyShouldNotBeNull()
        {
            new Action(() => new EncryptionKey(null)).Should().ThrowExactly<ArgumentException>();
        }
    }
}
