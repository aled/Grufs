using System;

namespace Wibblr.Grufs.Core
{
    public class AddressQueueByteSource : IByteSource
    {
        private readonly byte serializationVersion = 0;
        public byte Level { get; init; }
        public int TotalAddressCount { get; private set; }

        private Queue<byte> _queue = new Queue<byte>();
        private bool _isAddingCompleted;
        
        public AddressQueueByteSource(int level)
        {
            if (level > byte.MaxValue)
            {
                throw new Exception("Invalid level (infinite loop?)");
            }

            Level = (byte)level;

            _queue.Enqueue(serializationVersion);
            _queue.Enqueue(Level);
        }

        public bool Available()
        {
            return _queue.Any();
        }

        public bool IsCompleted()
        {
            return _isAddingCompleted && !_queue.Any();
        }

        public byte Next()
        {
            return _queue.Dequeue();
        }

        public void Add(Address address, long streamOffset, int chunkLength)
        {
            if (_isAddingCompleted)
            {
                throw new Exception();
            }

            var bytes = new BufferBuilder(Address.Length + 4)
                .AppendBytes(address)
                .AppendInt(chunkLength)
                .ToSpan();

            for (int i = 0; i < bytes.Length; i++)
            {
                _queue.Enqueue(bytes[i]);
            }

            TotalAddressCount++;
        }

        public void CompleteAdding()
        {
            _isAddingCompleted = true;
        }
    }
}
