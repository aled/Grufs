namespace Wibblr.Grufs.Cli
{
    public record NamedFlagArgDefinition(char? ShortName, string LongName, Action<bool> SetBool)
        : NamedArgDefinition(ShortName, LongName, x => SetBool(bool.Parse(x)))
    { 
    }

    public record NamedStringArgDefinition(char? ShortName, string LongName, Action<string> Set)
        : NamedArgDefinition(ShortName, LongName, Set)
    {
    }

    public record NamedArgDefinition(char? ShortName, string LongName, Action<string> Set)
        : ArgDefinition(Set)
    { 
    }

    /// <summary>
    /// A positional argument. The position can be specified from the start or the end.
    /// </summary>
    /// <param name="position">Postion of the argument, starting at 0. Count from -1 to specify the position from the end</param>
    public record PositionalStringArgDefinition(int Position, Action<string> Set) 
        : ArgDefinition(Set)
    {
    }

    public record ArgDefinition(Action<string> Set)
    {
    }
}
