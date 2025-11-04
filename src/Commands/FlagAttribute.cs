namespace CLX.Core.Commands;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
/// <summary> Declares metadata for a command flag property. </summary>
/// <remarks> Used by command implementations to describe how flags should be validated and
/// presented. </remarks>
public class FlagAttribute(string name) : Attribute
{
    /// <summary> The canonical name of the flag. </summary>
    public string Name { get; } = name;

    // TODO: XML docs needed
    public string? AlternateName { get; set; } = null;

    /// <summary> Whether the flag must be supplied by the user. </summary>
    public bool IsRequired { get; set; } = false;

    /// <summary> The minimum number of values required for the flag. </summary>
    public int MinValues { get; set; } = 0;

    /// <summary> The maximum number of values allowed for the flag. </summary>
    public int MaxValues { get; set; } = 0;

    /// <summary> A regex pattern that each value must satisfy. </summary>
    public string? ValueRegexPattern { get; set; } = null;
}