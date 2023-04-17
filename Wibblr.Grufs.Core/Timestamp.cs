using System.Globalization;
using System.Numerics;

namespace Wibblr.Grufs.Core
{
    public record struct Timestamp
    {
        private DateTime Value { get; init; }

        public Timestamp(DateTime datetime)
        {
            Value = datetime;
        }

        public Timestamp(string iso8601)
        {
            Value = DateTime.ParseExact(iso8601, "o", CultureInfo.InvariantCulture);
        }

        public Timestamp(BufferReader reader)
        {
            var ticks = reader.ReadLong();
            Value = new DateTime(ticks);
        }

        public static Timestamp Now => new Timestamp(DateTime.UtcNow);

        public static implicit operator DateTime(Timestamp timestamp) => timestamp.Value;

        public static bool operator <(Timestamp a, Timestamp b) => a.Value < b.Value;
        public static bool operator <=(Timestamp a, Timestamp b) => a.Value <= b.Value;
        public static bool operator >(Timestamp a, Timestamp b) => a.Value > b.Value;
        public static bool operator >=(Timestamp a, Timestamp b) => a.Value >= b.Value;

        public int GetSerializedLength() => new VarLong(Value.Ticks).GetSerializedLength();

        public void SerializeTo(BufferBuilder builder)
        {
            builder.AppendLong(Value.Ticks);
        }

        public override string ToString()
        {
            return Value.ToString("o");
        }

        public string ToString(string format)
        {
            return Value.ToString(format);
        }
    }
}