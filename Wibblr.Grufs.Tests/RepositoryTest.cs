using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using FluentAssertions;

using Xunit;

namespace Wibblr.Grufs.Tests
{
    public class RepositoryTest
    {
        [Fact]
        public void InitializeRepository()
        {
            var storage = new InMemoryChunkStorage();
            var r1 = new Repository(storage);
            r1.Initialize("hello");

            var r2 = new Repository(storage);
            r2.Open("hello");

            r1._masterKey.ToString().Should().Be(r2._masterKey.ToString());
        }
    }
}
