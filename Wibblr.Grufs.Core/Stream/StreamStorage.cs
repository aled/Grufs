﻿using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using Wibblr.Grufs.Logging;
using Wibblr.Grufs.Storage;

[assembly: InternalsVisibleTo("Wibblr.Grufs.Tests")]

namespace Wibblr.Grufs.Core
{
    public record StreamWriteResult(Address address, byte indexLevel, StreamWriteStats stats);

    public class StreamStorage
    {
        private IChunkStorage _chunkStorage;
        private IChunkSourceFactory _chunkSourceFactory;
        private ChunkEncryptor _chunkEncryptor;

        public StreamStorage(IChunkStorage chunkStorage, IChunkSourceFactory chunkSourceFactory, ChunkEncryptor chunkEncryptor)
        {
            _chunkStorage = chunkStorage;
            _chunkSourceFactory = chunkSourceFactory;
            _chunkEncryptor = chunkEncryptor;
        }

        public async Task<StreamWriteResult> WriteAsync(Stream stream, CancellationToken token)
        {
            var byteSource = new StreamByteSource(stream);
            var chunkSource = _chunkSourceFactory.Create(byteSource);
            var indexByteSources = new List<IndexByteSource>();
            var indexChunkSources = new List<IChunkSource>();
            var stats = new StreamWriteStats();
            var statusUpdateTime = DateTime.UtcNow - TimeSpan.FromMinutes(1);

            var (address, level) = await WriteAsync(chunkSource, level: 0, token);

            //Log.WriteLine(1, stats.ToString(Log.HumanFormatting));
            return new StreamWriteResult(address, level, stats);

            async Task<(Address, byte)> WriteAsync(IChunkSource chunkSource, byte level, CancellationToken token)
            {
                // When writing chunks, write to an index containing the addresses of all the chunks.
                // (The index will itself be recursively indexed, if it is more than one chunk in length)
                if (level > 100)
                {
                    throw new Exception();
                }

                IndexByteSource index;
                IChunkSource indexChunkSource;
                byte returnLevel = level;
                byte indexLevel = (byte)(level + 1);

                Address address;
                Address? indexAddress = null;
                do
                {
                    // Get next chunk if available. If not available, this must be a zero-length stream.
                    var (buf, streamOffset, len) = chunkSource.Available() 
                        ? chunkSource.Next() 
                        : (new byte[0], 0, 0);

                    var encryptedChunk = _chunkEncryptor.EncryptContentAddressedChunk(buf.AsSpan(0, len));
                    
                    switch (await _chunkStorage.PutAsync(encryptedChunk, OverwriteStrategy.Deny, token))
                    {
                        case PutStatus.Success:
                            if (level == 0)
                            {
                                stats.PlaintextLength += len;
                                stats.TransferredContentChunks++;
                                stats.TransferredContentBytes += encryptedChunk.Content.LongLength;
                                stats.TotalContentChunks++;
                                stats.TotalContentBytes += encryptedChunk.Content.LongLength;
                            }
                            else
                            {
                                stats.TransferredIndexChunks++;
                                stats.TransferredIndexBytes += encryptedChunk.Content.LongLength;
                                stats.TotalIndexChunks++;
                                stats.TotalIndexBytes += encryptedChunk.Content.LongLength;
                            }
                            break;

                        case PutStatus.OverwriteDenied:
                            if (level == 0)
                            {
                                stats.PlaintextLength += len;
                                stats.TotalContentChunks++;
                                stats.TotalContentBytes += encryptedChunk.Content.LongLength;
                            }
                            else
                            {
                                stats.TotalIndexChunks++;
                                stats.TotalIndexBytes += encryptedChunk.Content.LongLength;
                            }
                            break;

                        default:
                            throw new Exception("Failed to store chunk in repository");
                    }

                    var timeSinceLastStatusUpdate = DateTime.UtcNow - statusUpdateTime;
                    if (timeSinceLastStatusUpdate > TimeSpan.FromSeconds(0.5))
                    {
                        Log.WriteStatusLine(0, "  " + stats.ToString(Log.HumanFormatting));
                        statusUpdateTime = DateTime.UtcNow;
                    }
                    //Log.WriteLine(0, $"Wrote chunk, level {level}, offset {streamOffset}, length {bytes.Length}, compressed/encrypted length {encryptedChunk.Content.Length}, address {encryptedChunk.Address}");
                    //Log.WriteLine(0, level == 0 ? Encoding.ASCII.GetString(bytes) : $"   {Convert.ToHexString(bytes)}");
                    //Log.WriteLine(0, "-----------------");

                    address = encryptedChunk.Address;

                    if (indexByteSources.Count <= level)
                    {
                        index = new IndexByteSource(level + 1);
                        indexChunkSource = _chunkSourceFactory.Create(index);
                        indexByteSources.Add(index);
                        indexChunkSources.Add(indexChunkSource);
                    }
                    else
                    {
                        index = indexByteSources[level];
                        indexChunkSource = indexChunkSources[level];
                    }

                    index.Add(address, streamOffset, len);

                    if (indexChunkSource.Available())
                    {
                        (indexAddress, returnLevel) = await WriteAsync(indexChunkSource, indexLevel, token);
                    }
                } while (chunkSource.Available());

                if (chunkSource.IsCompleted())
                {
                    index.CompleteAdding();

                    // only write the address chunk if there was more than one address written to it.
                    if (indexChunkSource.Available() && index.TotalAddressCount > 1)
                    {
                        (indexAddress, returnLevel) = await WriteAsync(indexChunkSource, indexLevel, token);
                    }

                    Log.WriteStatusLine(0, "  " + stats.ToString(Log.HumanFormatting));
                }

                //Log.WriteLine(0, $"Returning from Write: address = {addressStreamAddress ?? address}, level = {returnLevel}");

                return (indexAddress ?? address, returnLevel);
            }
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
        public IAsyncEnumerable<ArrayBuffer> ReadAsync(int level, Address address, CancellationToken token)
        {
            return ReadAsync(level, address, new BufferBuilder[level], token);
        }

        private async IAsyncEnumerable<ArrayBuffer> ReadAsync(int level, Address address, BufferBuilder[] indexBuilders, [EnumeratorCancellation] CancellationToken token)
        {

            if (await _chunkStorage.GetAsync(address, token) is not EncryptedChunk chunk)
            {
                throw new Exception($"Address {address} not found in repository");
            }

            var buffer = _chunkEncryptor.DecryptContentAddressedChunk(chunk);
            //Log.WriteLine(0, $"Read chunk; level = {level} address = {address}");

            if (level == 0)
            {
                yield return buffer;
            }
            else
            {
                var reader = new BufferReader(buffer);

                if (indexBuilders[level - 1] == null)
                {
                    indexBuilders[level - 1] = new BufferBuilder(Address.Length + VarInt.MaxSerializedLength);
                
                    var serializationVersion = reader.ReadByte(); // serialization version

                    if (serializationVersion != 0)
                    {
                        throw new Exception();
                    }

                    var deserializedLevel = reader.ReadByte(); // level

                    if (deserializedLevel != level)
                    {
                        throw new Exception();
                    }
                    //Log.WriteLine(0, $"  serialization version = {serializationVersion}, deserializedLevel = {deserializedLevel}");
                }

                var indexBuilder = indexBuilders[level - 1];
                while (reader.RemainingLength() > 0)
                {
                    indexBuilder.AppendByte(reader.ReadByte());

                    // need to fill the indexBuilder with the address and a varint.
                    var indexBuffer = indexBuilder.ToBuffer();
                    if (indexBuffer.Length > Address.Length)
                    {
                        var b0 = indexBuffer.AsSpan()[Address.Length];
                        var leadingOnes = BitOperations.LeadingZeroCount(unchecked((uint)~(b0 << 24)));

                        if (indexBuffer.Length == Address.Length + 1 + leadingOnes)
                        {
                            var indexBuilderReader = new BufferReader(indexBuilder.ToBuffer());
                            var subAddress = indexBuilderReader.ReadAddress();
                            var chunkLength = indexBuilderReader.ReadInt();

                            indexBuilder.Clear();

                            var subchunkLevel = level - 1;

                            //Log.WriteLine(0, $"  Read subchunk addresses for level {subchunkLevel}: {subAddress}, chunkLength {chunkLength}");

                            await foreach (var subBuffer in ReadAsync(subchunkLevel, subAddress, indexBuilders, token))
                            {
                                yield return subBuffer;
                            }
                        }
                    }
                }
            }
        }
    }
}
