using System;

namespace Wibblr.Grufs
{
    public class Chain
    {
        public static readonly int headerLength = 32;
        public static readonly int itemLength = 40;
        private static readonly byte[] headerMagic = new[] { (byte)'g', (byte)'f', (byte)'c', (byte)255 };

        public List<Address> Addresses { get; private set; } = new List<Address>();
        public int Level;
        public ChunkType SubchunkType;
        private List<long> _streamOffsets = new List<long>();
        private List<long> _containedLengths = new List<long>();
        private int _capacity;

        public Chain(int capacity, int level)
        {
            if (level > byte.MaxValue)
            {
                throw new ArgumentOutOfRangeException();
            }

            _capacity = capacity;
            Level = level;
        }

        public void Append(Address address, long offset, long length)
        {
            Addresses.Add(address);
            _streamOffsets.Add(offset);
            _containedLengths.Add(length);
        }

        public long StreamOffset => _streamOffsets[0];

        public long StreamLength => _containedLengths.Sum();

        public int Count => Addresses.Count;

        public void Clear()
        {
            Addresses.Clear();
            _streamOffsets.Clear();
            _containedLengths.Clear();
        }

        public bool IsFull()
        {
            return Addresses.Count >= _capacity;
        }

        public ReadOnlySpan<byte> Serialize()
        {
            var builder = new BufferBuilder(headerLength + (itemLength * Addresses.Count))
                .AppendBytes(headerMagic)   // header to identify chain chunks against known-format content chunks
                .AppendByte((byte)0)       // serialization version
                .AppendByte((byte)Level)
                .AppendByte((byte)(Level == 0 ? ChunkType.Content : ChunkType.Chain))
                .AppendInt(Count)
                .AppendInt(_capacity)
                .AppendBytes(new byte[17]); // padding to reach 32 bytes of header

            for (int i = 0; i < Addresses.Count; i++)
            {
                builder.AppendBytes(Addresses[i].ToSpan());
                builder.AppendLong(_streamOffsets[i]);
            }

            return builder.ToSpan();
        }

        public static Chain Deserialize(Buffer buffer)
        {
            var reader = new BufferReader(buffer);

            if (!reader.ReadBytes(headerMagic.Length).SequenceEqual(headerMagic))
            {
                throw new Exception("Invalid header");
            }

            if (reader.ReadByte() != 0)
            {   
                throw new Exception("Invalid serialization version");
            }

            var level = reader.ReadByte();

            var subchunkType = (ChunkType)reader.ReadByte();
            if (subchunkType != ChunkType.Chain && subchunkType != ChunkType.Content)
            {
                throw new Exception($"Unknown chunk type: {subchunkType}");
            }

            var count = reader.ReadInt();
            var capacity = reader.ReadInt();
            var chain = new Chain(capacity, level);
            chain.SubchunkType = subchunkType;
            var length = 0L;
            for (int i = headerLength; i < buffer.Length; i += itemLength)
            {
                var subchunkAddress = new Address(buffer.AsSpan(i, Address.Length));
                var offset = reader.ReadLong();
                length += reader.ReadLong();

                chain.Append(subchunkAddress, offset, length);
            }

            if (chain.Count != count)
            {
                throw new Exception();
            }

            return chain;
        }
    }
}
