using System.Globalization;

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
                .OfType<NamedArgDefinition>()
                .Select(x => x.ShortName)
                .OfType<char>()
                .GroupBy(x => x)
                .Where(grp => grp.Count() > 1)
                .Select(grp => grp.Key.ToString());

            var longNameDuplicates = _argDefinitions
                .OfType<NamedArgDefinition>()
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

        public void Parse(IList<string> args)
        {
            for (int i = 0; i < args.Count; i++)
            {
                var arg = args[i];

                // Check if this is a long-name arg
                if (arg.StartsWith("--"))
                {
                    var argDefinition = _argDefinitions
                        .OfType<NamedArgDefinition>()
                        .FirstOrDefault(x => x.LongName == arg.Substring(2));

                    if (argDefinition is NamedFlagArgDefinition f)
                    {
                        f.Set("true");
                        continue;
                    }
                    else if (argDefinition is NamedStringArgDefinition s)
                    {
                        s.Set(args[++i]);
                        continue;
                    }
                }
                else if (arg.StartsWith('-'))
                {
                    for (int j = 1; j < arg.Length; j++)
                    {
                        var argDefinition = _argDefinitions
                            .OfType<NamedArgDefinition>()
                            .FirstOrDefault(x => x.ShortName == arg[j]);

                        if (argDefinition is NamedFlagArgDefinition f)
                        {
                            f.Set("true");
                        }
                        else if (argDefinition is NamedStringArgDefinition s && j == arg.Length - 1)
                        {
                            s.Set(args[++i]);
                        }
                        else
                        {
                            throw new UsageException($"Unhandled argument '{arg[j]}'");
                        }
                    }
                    continue;
                }
                else
                {
                    // Check whether this is a positional argument
                    var positionalArg = _argDefinitions
                        .OfType<PositionalStringArgDefinition>()
                        .FirstOrDefault(x => x.Position == i || x.Position == i - args.Count);

                    if (positionalArg != null)
                    {
                        positionalArg.Set(arg);
                        continue;
                    }
                }
                throw new UsageException();
            }
        }
    }
}