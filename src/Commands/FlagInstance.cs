namespace CLX.Core.Commands;

/// <summary> Immutable flag object parsed from the command line. </summary>
/// <remarks> Contains the flag's canonical name and the list of values supplied. </remarks>
public sealed record FlagObject(string Name, string? AlternateName, IReadOnlyList<string> Values) : IFlagObject;