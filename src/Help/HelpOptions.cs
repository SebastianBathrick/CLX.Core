using CLX.Core.Commands;

namespace CLX.Core.Help;

/// <summary>
/// Global configuration for Help command output writers.
/// </summary>
public static class HelpOptions
{
    /// <summary> Writer used by Help for standard output. </summary>
    public static ITextWriter OutputWriter { get; set; } = NullTextWriter.Instance;

    /// <summary> Writer used by Help for error output. </summary>
    public static ITextWriter ErrorOutputWriter { get; set; } = NullTextWriter.Instance;
}


