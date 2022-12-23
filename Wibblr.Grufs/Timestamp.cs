namespace Wibblr.Grufs
{
    public record Timestamp
    {
        public DateTime Value;

        public Timestamp(BufferReader reader)
        {
            var ticks = reader.ReadLong();
            Value = new DateTime(ticks);
        }

        public int GetSerializedLength() => 8;

        public void SerializeTo(BufferBuilder builder)
        {
            builder.CheckBounds(8);
            builder.AppendLong(Value.Ticks);
        }
    }
}