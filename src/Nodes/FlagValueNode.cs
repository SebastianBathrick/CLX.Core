namespace CLX.Core.Nodes;

/// <summary> Node that represents a flag argument. FlagArgumentNodes are children of FlagNodes and
/// represent the arguments that are passed to the flag. </summary>
internal sealed record FlagValueNode(string Value) : INode
{
    public override string ToString() => $"Arg({Value})";
}

