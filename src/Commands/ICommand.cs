namespace CLX.Core.Commands;

/// <summary> Defines the contract for a CLX command executable by the runtime. </summary>
/// <remarks> Each implementation is discovered via reflection and executed with a parsed
/// <see cref="ICommandContext"/>. </remarks>
public interface ICommand
{
    /// <summary> The command name used on the command line. </summary>
    string Name { get; }

    /// <summary> A brief, user-facing description of what the command does. </summary>
    string Description { get; }

    /// <summary> The writer used for standard output. </summary>
    ITextWriter Output { get; }

    /// <summary> The writer used for error output. </summary>
    ITextWriter ErrorOutput { get; }

    /// <summary> The working directory to use for the command. </summary>
    string WorkingDirectory { get; }


    int Execute(ICommandContext context, string workingDirectory = "");
}
