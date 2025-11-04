namespace CLX.Core.Nodes;

/// <summary> Node that represents a flag. FlagNodes branch off to child nodes that represent the
/// arguments that are passed to the flag. </summary>
internal sealed record FlagNode(string GivenName, IReadOnlyList<ValueNode> ValueNodes)
{
    public override string ToString() => ValueNodes.Count > 0
        ? $"Flag({GivenName}): {string.Join(", ", ValueNodes)}"
        : $"Flag({GivenName})";
}

