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
        private readonly Dictionary<string, byte[]?> _changes;

        public Collection(VersionedDictionary storage, string name)
        {
            _storage = storage;
            _name = Encoding.UTF8.GetBytes(name);
            _changes = new Dictionary<string, byte[]?>();
        }

        public IEnumerable<byte[]> Values()
        {
            Dictionary<string, byte[]> dict = new Dictionary<string, byte[]>();

            // Each buffer here represents a number of changesets
            foreach (var buffer in _storage.Values(_name).Select(x => x.Item2))
            {
                var reader = new BufferReader(buffer);

                var serializationVersion = reader.ReadByte(); // serialization version
                var changeCount = reader.ReadVarInt().Value;

                for (int i = 0; i < changeCount; i++)
                {
                    var operation = reader.ReadByte();

                    switch (operation)
                    {
                        case 0:
                            {
                                var keyLength = reader.ReadVarInt().Value;
                                var key = reader.ReadBytes(keyLength);
                                var valueLength = reader.ReadVarInt().Value;
                                var value = reader.ReadBytes(valueLength);

                                dict[Convert.ToHexString(key)] = value.ToArray();
                            }
                            break;

                        case 1:
                            {
                                var keyLength = reader.ReadVarInt().Value;
                                var key = reader.ReadBytes(keyLength);

                                dict.Remove(Convert.ToHexString(key));
                            }
                            break;
                    }
                }
            }

            return dict.Values;
        }

        public void PrepareUpdate(byte[] lookupKey, byte[] value)
        {
            _changes[Convert.ToHexString(lookupKey)] = value;
        }

        public void PrepareDelete(byte[] lookupKey) 
        {
            _changes[Convert.ToHexString(lookupKey)] = null;
        }

        private int GetSerializedLength(string key, byte[]? value)
        {
            if (value == null)
            {
                // i.e. a delete
                return 1 + new VarInt(key.Length / 2).GetSerializedLength() + key.Length;
            }

            // i.e. a create or update
            return 1 + new VarInt(key.Length / 2).GetSerializedLength() + key.Length
                     + new VarInt(value.Length).GetSerializedLength() + value.Length;
        }

        private IEnumerable<int> GetSerializedLengths()
        {
            foreach (var kv in _changes)
            {
                yield return GetSerializedLength(kv.Key, kv.Value);
            }
        }

        private int GetSerializedLength()
        {
            return 1 + new VarInt(_changes.Count).GetSerializedLength() + GetSerializedLengths().Sum();
        }

        public long WriteChanges(long previousVersion)
        {
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

            builder.AppendByte(0);
            builder.AppendVarInt(new VarInt(_changes.Count()));
            foreach (var kv in _changes)
            {
                if (kv.Value != null)
                {
                    builder.AppendByte(0);
                    builder.AppendVarInt(new VarInt(kv.Key.Length / 2));
                    builder.AppendBytes(Convert.FromHexString(kv.Key));
                    builder.AppendVarInt(new VarInt(kv.Value.Length));
                    builder.AppendBytes(kv.Value);
                }
                else
                {
                    builder.AppendByte(1);
                    builder.AppendVarInt(new VarInt(kv.Key.Length / 2));
                    builder.AppendBytes(Convert.FromHexString(kv.Key));
                }
            }

            if (_storage.TryPutValue(_name, nextVersion, builder.ToSpan()))
            {
                return nextVersion;
            }

            throw new Exception();
        }
    }
}
