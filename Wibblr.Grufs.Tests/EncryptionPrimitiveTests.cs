using FluentAssertions;

using Wibblr.Grufs.Encryption;
using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Tests
{
    public class EncryptionPrimitiveTests
    {
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
