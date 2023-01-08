using System;
using System.Text;

namespace Wibblr.Grufs.Core
{
    /// <summary>
    /// Enable storage of a collection of objects within an immutable repository.
    /// 
    /// Works by storing changes to the collection in versioned dictionary storage. 
    /// </summary>
    public class CollectionStorage
    {
        private readonly VersionedDictionaryStorage _storage;
        private readonly byte[] _name;
        private readonly Dictionary<string, byte[]?> _changes;

        public CollectionStorage(VersionedDictionaryStorage storage, string name)
        {
            _storage = storage;
            _name = Encoding.UTF8.GetBytes(name);
            _changes = new Dictionary<string, byte[]?>();
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
            return 1 + GetSerializedLengths().Sum();
        }

        public long WriteChanges(long previousVersion)
        {
            var nextVersion = _storage.GetNextSequenceNumber(_name, previousVersion);

            if (nextVersion != previousVersion + 1) 
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
            foreach (var kv in _changes)
            {
                if (kv.Value != null)
                {
                    builder.AppendByte(0);
                    builder.AppendVarInt(new VarInt(kv.Key.Length));
                    builder.AppendBytes(Convert.FromHexString(kv.Key));
                    builder.AppendVarInt(new VarInt(kv.Value.Length));
                    builder.AppendBytes(kv.Value);
                }
                else
                {
                    builder.AppendByte(1);
                    builder.AppendVarInt(new VarInt(kv.Key.Length));
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
