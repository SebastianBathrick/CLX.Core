using System.Collections.Generic;

namespace CLX.Core.Nodes;

/// <summary> Root node that represents a command. CommandRootNodes branch off to child nodes that
/// represent flags that trailed behind the command name. </summary>
internal sealed record CommandNode(string Name, IReadOnlyList<INode> FlagNodes) : INode
{
    public override string ToString() => $"Command({Name})\n\n\t{string.Join("\n\n\t", FlagNodes)}\n";
}

