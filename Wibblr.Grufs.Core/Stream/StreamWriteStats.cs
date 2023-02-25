namespace Wibblr.Grufs.Core
{
    public class StreamWriteStats
    {
        public long PlaintextLength;
        public long TotalContentChunks;
        public long TotalIndexChunks;
        public long TransferredContentChunks;
        public long TransferredIndexChunks;
        public long TotalContentBytes;
        public long TotalIndexBytes;
        public long TransferredContentBytes;
        public long TransferredIndexBytes;

        public override string ToString()
        {
            //return $"length:{PlaintextLength}, content chunks:{TransferredFileChunks}/{TotalFileChunks}, index chunks:{TransferredFileIndexChunks}/{TotalFileIndexChunks}, content bytes:{TransferredFileBytes}/{TotalFileBytes}, index bytes:{TransferredFileIndexBytes}/{TotalFileIndexBytes}";
            var compressedPercent = PlaintextLength > 0 ? 100 * TotalBytes / (decimal)PlaintextLength : 0m;
            var transferredPercent = PlaintextLength > 0 ? 100 * TransferredBytes / (decimal)PlaintextLength : 0m;

            return $"Length: {PlaintextLength}; compressed/encrypted {TotalBytes} ({decimal.Round(compressedPercent, 1)}%), transferred {TransferredBytes} ({decimal.Round(transferredPercent, 2)}%), deduplication reduction {DeduplicationPercent}";
        }

        public void Add(StreamWriteStats other)
        {
            PlaintextLength += other.PlaintextLength;
            TotalContentChunks += other.TotalContentChunks;
            TotalIndexChunks += other.TotalIndexChunks;
            TransferredContentChunks += other.TransferredContentChunks;
            TransferredIndexChunks += other.TransferredIndexChunks;
            TotalContentBytes += other.TotalContentBytes;
            TotalIndexBytes += other.TotalIndexBytes;
            TransferredContentBytes += other.TransferredContentBytes;
            TransferredIndexBytes += other.TransferredIndexBytes; 
        }

        public long TransferredBytes => TransferredContentBytes + TransferredIndexBytes;

        public long TotalBytes => TotalContentBytes + TotalIndexBytes;

        /// <summary>
        /// A deduplication ratio of 0% means that all of the data was actually transferred
        /// 100% means that no data was transferred
        /// </summary>
        public string DeduplicationPercent => TotalBytes == 0 ? "--" : $"{100 - Math.Round(100 * (double)TransferredBytes)/TotalBytes, 2:0.0}%";
    }
}
