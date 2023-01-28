using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Wibblr.Grufs.Core;

namespace Wibblr.Grufs.Filesystem
{
    /// <summary>
    /// Store an entire filesystem snapshot in a single stream. Used for backups.
    /// </summary>
    public class ImmutableFilesystem
    {
        private Repository _repository;

        public ImmutableFilesystem(Repository repository)
        {
            _repository = repository;
        }


    }
}
