namespace CLX.Core.Nodes;

/// <summary> Root node that represents a command. CommandRootNodes branch off to child nodes that
/// represent flags that trailed behind the command name and positional values. </summary>
internal sealed record CommandNode(string Name, IReadOnlyList<FlagNode> FlagNodes, IReadOnlyList<ValueNode> PositionalNodes)
{
    public override string ToString() =>
        $"Command({Name})\n\n\tFlags: {string.Join(", ", FlagNodes)}\n\tArgs: {string.Join(", ", PositionalNodes)}\n";
}

