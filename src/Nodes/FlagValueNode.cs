namespace CLX.Core.Nodes;

/// <summary> Node that represents a flag argument. FlagArgumentNodes are children of FlagNodes and
/// represent the arguments that are passed to the flag. </summary>
internal sealed record ValueNode(string Value)
{
    public override string ToString() => $"Arg({Value})";
}

