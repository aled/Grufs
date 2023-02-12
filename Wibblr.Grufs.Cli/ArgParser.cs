namespace Wibblr.Grufs.Cli
{
    public class ArgParser
    {
        private ArgDefinition[] _argDefinitions;

        private List<string> invalidArgs = new List<string>();

        public ArgParser(ArgDefinition[] argDefinitions)
        {
            _argDefinitions = argDefinitions;
            Validate();
        }

        public void Validate()
        {
            // Warn on duplicate definitions
            IEnumerable<string> shortNameDuplicates = _argDefinitions
                .Select(x => x.ShortName)
                .OfType<char>()
                .GroupBy(x => x)
                .Where(grp => grp.Count() > 1)
                .Select(grp => grp.Key.ToString());

            var longNameDuplicates = _argDefinitions
                .GroupBy(x => x.LongName)
                .Where(grp => grp.Count() > 1)
                .Select(grp => grp.Key);

            var duplicates = new HashSet<string>(shortNameDuplicates.Concat(longNameDuplicates))
                .OrderBy(x => x)
                .ToList();

            if (duplicates.Any())
            {
                throw new ArgumentException($"Duplicate argument definition(s): '{string.Join("',' ", duplicates)}'");
            }
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