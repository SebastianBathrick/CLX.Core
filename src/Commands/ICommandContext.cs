using CLX.Core.Writers;
using CLX.Core.Pipeline.Helpers;

namespace CLX.Core.Commands;

/// <summary> Represents a validated invocation context for a single command. </summary>
/// <remarks> Produced by the parser and passed to command implementations at execution time. </remarks>
public interface ICommandContext
{
    /// <summary> The name of the command to execute. </summary>
    string CommandName { get; }

    /// <summary> The collection of parsed flag instances for this command. </summary>
    IReadOnlyList<IFlagObject> Flags { get; }

    /// <summary> Writer used for standard output. </summary>
    ITextWriter Output { get; }

    /// <summary> Writer used for error output. </summary>
    ITextWriter ErrorOutput { get; }

    /// <summary> The working directory to associate with the command execution. </summary>
    string WorkingDirectory { get; }

    /// <summary> The collection of positional arguments supplied to this command. </summary>
    IReadOnlyList<string> Arguments { get; }


    /// <summary> Tries to retrieve a flag by name and type. </summary>
    /// <param name="context">The command context that holds flags.</param>
    /// <param name="flagName">The flag name to search for.</param>
    /// <param name="flagInstance">On success, the typed flag instance; otherwise, null.</param>
    /// <returns>True if the flag was found and of the requested type; otherwise, false.</returns>
    static bool TryGetFlag<T>(ICommandContext context, string flagName, out T? flagInstance) where T : class, IFlagObject
    {
        foreach (var flag in context.Flags)
            if ((flag.Name == flagName || flag.AlternateName == flagName) && flag is T castedFlag)
            {
                flagInstance = castedFlag;
                return true;
            }

        flagInstance = default;
        return false;
    }

    /// <summary> Try to read a typed positional argument at the given index. </summary>
    static bool TryGetArgument<T>(ICommandContext context, int index, out T value)
    {
        value = default!;
        return index >= 0 && index < context.Arguments.Count && TypeConversion.TryConvert(context.Arguments[index], out value, out _);
    }

    /// <summary> Try to read all remaining positional arguments from a start index as a typed list. </summary>
    static bool TryGetArguments<T>(ICommandContext context, int startIndex, out IReadOnlyList<T> values)
    {
        values = Array.Empty<T>();
        if (startIndex < 0 || startIndex > context.Arguments.Count)
            return false;
        var slice = context.Arguments.Skip(startIndex);
        return TypeConversion.TryConvertMany(slice, out values, out _);
    }
}
