using System.Globalization;

namespace Wibblr.Grufs.Core
{
    public record struct Timestamp
    {
        private DateTime Value { get; init;  }

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

        public int GetSerializedLength() => 8;

        public void SerializeTo(BufferBuilder builder)
        {
            builder.CheckBounds(8);
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