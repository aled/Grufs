namespace Wibblr.Grufs
{
    public enum OverwriteStrategy
    {
        Unknown = 0,
        DenyWithError = 1,
        DenyWithSuccess = 2,
        VerifyChecksum = 3,
        VerifyContent = 4,
        Allow = 5
    }
}