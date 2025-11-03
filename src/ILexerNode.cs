namespace CLX.Lexing;

/// <summary> Tree node that represents a command, flag, or flag argument. </summary>
internal interface ILexerNode
{
    public string ToString();
}

/// <summary> Root node that represents a command. CommandRootNodes branch off to child nodes that 
/// represent flags that trailed behind the command name. </summary>
sealed record CommandNode(string Name, IReadOnlyList<ILexerNode> FlagNodes) : ILexerNode
{
    public override string ToString() => $"Command({Name})\n\n\t{string.Join("\n\n\t", FlagNodes)}\n";
}

/// <summary> Node that represents a flag argument. FlagArgumentNodes are children of FlagNodes and 
/// represent the arguments that are passed to the flag. </summary>
sealed record FlagArgumentNode(string Value) : ILexerNode
{
    public override string ToString() => $"Arg({Value})";
}

/// <summary> Node that represents a flag. FlagNodes branch off to child nodes that represent the 
/// arguments that are passed to the flag. </summary>
sealed record FlagNode(string Name, IReadOnlyList<ILexerNode> FlagArgNodes) : ILexerNode
{
    public override string ToString() => FlagArgNodes.Count > 0 ? $"Flag({Name}): {string.Join(", ", FlagArgNodes)}" : $"Flag({Name})";
}