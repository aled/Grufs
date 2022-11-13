namespace Wibblr.Grufs
{
    public interface IStorage
    {
        void Write(string key, byte[] data);

        byte[] Read(string key);
    }
}