﻿using System;
using System.Runtime.CompilerServices;

using Wibblr.Grufs.Encryption;

[assembly: InternalsVisibleTo("Wibblr.Grufs.Tests")]

namespace Wibblr.Grufs
{
    public class StreamStorage
    {
        private ChunkEncryptor _chunkEncryptor = new ChunkEncryptor();
        private IChunkStorage _chunkStorage;
        private int _chunkSize;

        public StreamStorage(IChunkStorage chunkStorage, int chunkSize = 128 * 1024)
        {
            if (chunkSize < 128)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize));
            }

            _chunkStorage = chunkStorage;
            _chunkSize = chunkSize;
        }

        public (Address, ChunkType) Write(KeyEncryptionKey contentKeyEncryptionKey, HmacKey hmacKey, Stream stream)
        {
            var nodes = new List<ChunkTreeNode>(); // keep state for one node for each level of the tree

            // Node buffer has 32 bytes header, then each contained address is 32 bytes plus 8 for the contained length.
            var nodeCapacity = (_chunkSize - ChunkTreeNode.headerLength) / ChunkTreeNode.itemLength;
            if (nodeCapacity < 2)
            {
                throw new Exception("Invalid node capacity");
            }

            void AppendToNode(Address address, int level, long streamOffset, long streamLength)
            {
                if (level > byte.MaxValue)
                {
                    throw new Exception("Invalid level (infinite loop?)");
                }

                if (nodes.Count <= level)
                {
                    nodes.Add(new ChunkTreeNode(nodeCapacity, level));
                }

                var node = nodes[level];
                if (node.IsFull())
                {
                    WriteNode(node);
                    node.Clear();
                }

                node.Append(address, streamOffset, streamLength);
            }

            Address WriteNode(ChunkTreeNode node)
            {
                var content = node.Serialize();
                var nodeChunk = _chunkEncryptor.EncryptChunk(contentKeyEncryptionKey, hmacKey, content);

                if (!_chunkStorage.TryPut(nodeChunk, OverwriteStrategy.DenyWithSuccess))
                {
                    throw new Exception("Failed to store chunk in repository");
                }

                Console.WriteLine($"Wrote chunk tree node (level {node.Level}, count {node.Count}, streamoffset {node.StreamOffset}, streamlength {node.StreamLength})");
                AppendToNode(nodeChunk.Address, node.Level + 1, node.StreamOffset, node.StreamLength);
                return nodeChunk.Address;
            }

            void Write(ReadOnlySpan<byte> bytes, long streamOffset)
            {
                var encryptedChunk = _chunkEncryptor.EncryptChunk(contentKeyEncryptionKey, hmacKey, bytes);

                if (!_chunkStorage.TryPut(encryptedChunk, OverwriteStrategy.DenyWithSuccess))
                {
                    throw new Exception("Failed to store chunk in repository");
                }
                Console.WriteLine($"Wrote content chunk (offset {streamOffset}, length {bytes.Length})");
                AppendToNode(encryptedChunk.Address, level: 0, streamOffset, bytes.Length);
            }

            var buf = new byte[_chunkSize];
            var bytesRead = 0;
            var bufContentLength = 0;
            var streamOffset = 0L;

            while ((bytesRead = stream.Read(buf, bufContentLength, _chunkSize - bufContentLength)) != 0)
            {
                bufContentLength += bytesRead;
                if (_chunkSize == bufContentLength)
                {
                    Write(buf, streamOffset);
                    streamOffset += bufContentLength;
                    bufContentLength = 0;
                }
            }

            if (bufContentLength > 0)
            {
                Write(buf.AsSpan(0, bufContentLength), streamOffset);
            }

            var node = nodes[0];
            var address = node.Addresses[0];
            if (nodes.Count == 1 && nodes[0].Count == 1)
            {
                return (address, ChunkType.Content);
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                node = nodes[i];

                // Write this node, unless it is the root (highest index in nodes list), and it contains only one chunk
                if (i < nodes.Count - 1 || node.Count > 1)
                {
                    address = WriteNode(node);
                }
            }

            return (address, ChunkType.ChunkTreeNode);
        }

        /// <summary>
        /// TODO: implement a custom stream class that will allow random access without storing the entire stream in memory
        /// </summary>
        /// <param name="type"></param>
        /// <param name="contentKeyEncryptionKey"></param>
        /// <param name="hmacKey"></param>
        /// <param name="address"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public IEnumerable<Buffer> Read(ChunkType type, KeyEncryptionKey contentKeyEncryptionKey, HmacKey hmacKey, Address address)
        {
            if (!_chunkStorage.TryGet(address, out var chunk))
            {
                throw new Exception($"Address {address} not found in repository");
            }

            var buffer = _chunkEncryptor.DecryptChunkAndVerifyAddress(chunk, contentKeyEncryptionKey, hmacKey);

            if (type == ChunkType.Content)
            {
                yield return buffer;
            }
            else if (type == ChunkType.ChunkTreeNode)
            {
                var node = ChunkTreeNode.Deserialize(buffer);

                foreach (var subAddress in node.Addresses) 
                {
                    foreach (var subBuffer in Read(node.SubchunkType, contentKeyEncryptionKey, hmacKey, subAddress))
                    {
                        yield return subBuffer;
                    }
                }
            }
            else throw new Exception("Unknown chunk type");
        }
    }
}
