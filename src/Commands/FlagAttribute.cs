namespace CLX.Core.Commands;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class FlagAttribute(string name) : Attribute
{
    public string Name { get; } = name;
    public bool IsRequired { get; set; } = false;
    public bool IsVariadic { get; set; } = false;
    public int MinValues { get; set; } = 0;
    public int MaxValues { get; set; } = 1;
    public string ValidRegexPattern { get; set; } = ".*";
}