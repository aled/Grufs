namespace Wibblr.Grufs.Cli
{
    public class ArgDefinition
    {
        public char? ShortName;
        public string LongName;
        public bool IsFlag;
        public Action<string> Set;

        public ArgDefinition(char? shortName, string longName, Action<string> set, bool isFlag = false)
        {
            ShortName = shortName;
            LongName = longName;
            IsFlag = isFlag;
            Set = set;
        }
    }
}