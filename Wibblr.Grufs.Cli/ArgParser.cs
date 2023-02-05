using Microsoft.VisualBasic;

namespace Wibblr.Grufs.Cli
{
    public class ArgParser
    {
        private ArgDefinition[] _argDefinitions;

        private List<string> invalidArgs = new List<string>();

        public ArgParser(ArgDefinition[] argDefinitions)
        {
            // Warn on duplicate definitions
            var shortNameDuplicates = argDefinitions
                .GroupBy(x => x.ShortName)
                .Where(grp => grp.Count() > 1)
                .Select(grp => grp.Key);

            foreach (var d in shortNameDuplicates)
            {
                Console.WriteLine("Duplicate argument short name: 'd'");
            }

            var longNameDuplicates = argDefinitions
                .GroupBy(x => x.LongName)
                .Where(grp => grp.Count() > 1)
                .Select(grp => grp.Key);

            foreach (var d in longNameDuplicates)
            {
                Console.WriteLine("Duplicate argument long name: 'd'");
            }

            _argDefinitions = argDefinitions;
        }

        private void ParseArg(string name, IEnumerator<string>? e)
        {
            var arg = name.Length switch
            {
                1 => _argDefinitions.FirstOrDefault(x => x.ShortName == name[0]),
                > 1 => _argDefinitions.FirstOrDefault(x => x.LongName == name),
                _ => throw new Exception()
            };

            if (arg == null)
            {
                invalidArgs.Add(name);
            }
            else if (arg.IsFlag)
            {
                arg.Set(true.ToString());
            }
            else
            {
                if (e == null)
                {
                    throw new Exception($"No value available for arg '{name}'");
                }
                if (!e.MoveNext())
                {
                    throw new Exception($"No value supplied for arg '{name}'");
                }
                arg.Set(e.Current);
            }
        }

        public void Parse(IEnumerable<string> args)
        {
            var e = args.GetEnumerator();
            while (e.MoveNext())
            {
                if (e.Current.StartsWith("--"))
                {
                    ParseArg(e.Current.Substring(2), e);
                }
                else if (e.Current.StartsWith("-"))
                {
                    var chars = e.Current.Substring(1);
                    foreach (char c in chars)
                    {
                        ParseArg(c.ToString(), chars.Length > 1 ? null : e);
                    }
                }
            }
        }
    }
}