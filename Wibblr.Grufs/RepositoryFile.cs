namespace Wibblr.Grufs
{
    internal class RepositoryFile 
    {
        /// <summary>
        /// It will be necessary to read all older versions of this class
        /// </summary>
        public PathString Name { get; set; }

        public Address Address { get; set; }

        public Timestamp LastModifiedTimestamp { get; set; }

        public int GetSerializedLength() => 
            Name.GetSerializedLength() + 
            Address.Length + 
            LastModifiedTimestamp.GetSerializedLength();
    }
}
