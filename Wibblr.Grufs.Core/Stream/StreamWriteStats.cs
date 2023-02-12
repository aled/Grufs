namespace Wibblr.Grufs.Core
{
    public class StreamWriteStats
    {
        public long TotalContentChunkCount;
        public long TotalIndexChunkCount;
        public long DedupedContentChunkCount;
        public long DedupedIndexChunkCount;
        public long PlaintextLength;
        public long TotalStoredContentLength;
        public long TotalStoredIndexLength;
        public long DedupedStoredContentLength;
        public long DedupedStoredIndexLength;

        public override string ToString()
        {
            return $"length:{PlaintextLength}, content chunks:{DedupedContentChunkCount}/{TotalContentChunkCount}, index chunks:{DedupedIndexChunkCount}/{TotalIndexChunkCount}, content bytes:{DedupedStoredContentLength}/{TotalStoredContentLength}, index bytes:{DedupedStoredIndexLength}/{TotalStoredIndexLength}";
        }
    }
}
