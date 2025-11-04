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
}
