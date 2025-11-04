namespace CLX.Core.Commands;

/// <summary> Represents a parsed flag and its values. </summary>
/// <remarks> Instances are provided on <see cref="ICommandContext"/> to command executors. </remarks>
public interface IFlagObject
{
    /// <summary> The canonical flag name. </summary>
    string Name { get; }

    /// <summary> The alternate flag name. </summary>
    string? AlternateName { get; }

    /// <summary> The values supplied for the flag, in order. </summary>
    IReadOnlyList<string> Values { get; }
}
