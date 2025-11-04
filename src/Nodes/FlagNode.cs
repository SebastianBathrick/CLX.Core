namespace CLX.Core.Nodes;

/// <summary> Node that represents a flag. FlagNodes branch off to child nodes that represent the
/// arguments that are passed to the flag. </summary>
internal sealed record FlagNode(string Name, IReadOnlyList<INode> FlagArgNodes) : INode
{
    public override string ToString() => FlagArgNodes.Count > 0
        ? $"Flag({Name}): {string.Join(", ", FlagArgNodes)}"
        : $"Flag({Name})";
}

