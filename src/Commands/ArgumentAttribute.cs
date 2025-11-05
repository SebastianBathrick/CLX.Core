namespace CLX.Core.Commands;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
/// <summary> Declares metadata for a flag value on a command. </summary>
public sealed class FlagValueAttribute(int index) : Attribute
{
    /// <summary> Zero-based position index for this argument. </summary>
    public int Index { get; } = index;

    /// <summary> Display name used in help/usage. Defaults to "arg{Index}" if not set. </summary>
    public string? Name { get; set; } = null;

    /// <summary> Whether at least one value is required for this argument. </summary>
    public bool IsRequired { get; set; } = false;

    /// <summary> Minimum number of values for this argument. </summary>
    public int MinValues { get; set; } = 0;

    /// <summary> Maximum number of values for this argument. Use int.MaxValue for variadic. </summary>
    public int MaxValues { get; set; } = 1;

    /// <summary> A regex pattern that each value must satisfy. </summary>
    public string? ValueRegexPattern { get; set; } = null;
}


