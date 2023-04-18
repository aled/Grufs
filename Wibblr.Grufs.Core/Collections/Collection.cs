using System;
using System.Text;

namespace Wibblr.Grufs.Core
{
    /// <summary>
    /// Enable storage of a collection of objects within an immutable repository.
    /// 
    /// Works by storing changes to the collection in versioned dictionary storage. 
    /// </summary>
    public class Collection
    {
        private readonly VersionedDictionary _storage;
        private readonly byte[] _name;

        // Store the changes in a dictionary, so that later changes to a key's value
        // will overwrite the previous version.
        private readonly Dictionary<byte[], byte[]?> _changes;

        public Collection(VersionedDictionary storage, string name)
        {
            _storage = storage;
            _name = Encoding.UTF8.GetBytes(name);
            _changes = new Dictionary<byte[], byte[]?>(new ByteArrayEqualityComparer());
        }

        public IEnumerable<byte[]> Values()
        {
            var dict = new Dictionary<byte[], byte[]?>(new ByteArrayEqualityComparer());

            // Each buffer here represents a number of changesets
            foreach (var buffer in _storage.Values(_name).Select(x => x.value))
            {
                var reader = new BufferReader(buffer);

                var serializationVersion = reader.ReadByte();
                var changeCount = reader.ReadInt();

                for (int i = 0; i < changeCount; i++)
                {
                    var operation = reader.ReadByte();

                    switch (operation)
                    {
                        case 0:
                            {
                                var key = reader.ReadSpan().ToArray();
                                var value = reader.ReadSpan().ToArray();
                                dict[key] = value;
                            }
                            break;

                        case 1:
                            {
                                var key = reader.ReadSpan().ToArray();
                                dict.Remove(key);
                            }
                            break;
                    }
                }
            }

            return dict.Values;
        }

        public void PrepareUpdate(byte[] lookupKey, byte[] value)
        {
            _changes[lookupKey] = value;
        }

        public void PrepareDelete(byte[] lookupKey) 
        {
            _changes[lookupKey] = null;
        }

        private IEnumerable<int> GetSerializedLengths()
        {
            foreach (var kv in _changes)
            {
                var key = kv.Key;
                var value = kv.Value;

                if (value == null)
                {
                    // i.e. a delete
                    yield return
                        1 + // update/delete flag
                        key.GetSerializedLength();
                }
                else
                {
                    // i.e. a create or update
                    yield return 
                        1 + // update/delete flag
                        key.GetSerializedLength() +
                        value.GetSerializedLength();
                }
            }
        }

        private int GetSerializedLength()
        {
            return 1 +
                _changes.Count.GetSerializedLength() +
                GetSerializedLengths().Sum();
        }

        public long WriteChanges(long previousVersion)
        {
            // Optimistic concurrency - caller must specify the previous version
            // to ensure the collection has not been updated concurrently.
            var nextVersion = _storage.GetNextSequenceNumber(_name, previousVersion);

            if (previousVersion > 0 && nextVersion != previousVersion + 1) 
            {
                throw new Exception("Collection has changed since last retrieved - cannot update");
            }

            // Serialize as follows:
            // length value
            // 1      0 = serialization version
            // 1      0 = update/create, 1 = delete
            // 1-5    varint length of key
            // n      key
            // [following only for update/create]
            // 1-5    varint length of value
            // n      value

            var builder = new BufferBuilder(GetSerializedLength());

            builder.AppendByte(0); // serialization version
            builder.AppendInt(_changes.Count());
            foreach (var kv in _changes)
            {
                var key = kv.Key;
                var value = kv.Value;

                if (value != null)
                {
                    builder.AppendByte(0); // update flag
                    builder.AppendSpan(key);
                    builder.AppendSpan(value);
                }
                else
                {
                    builder.AppendByte(1); // delete flag
                    builder.AppendSpan(key);
                }
            }

            // This may fail if there is a concurrent update
            // TODO: handle this case
            if (_storage.TryPutValue(_name, nextVersion, builder.ToSpan()))
            {
                return nextVersion;
            }

            throw new Exception();
        }
    }
}
