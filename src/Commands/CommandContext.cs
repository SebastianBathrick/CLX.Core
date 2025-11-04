namespace CLX.Core.Commands;
public sealed record CommandContext : ICommandContext
{
    public CommandContext(
        string commandName,
        IReadOnlyList<IFlagObject> flags,
        ITextWriter output,
        ITextWriter errorOutput,
        string workingDirectory,
        IReadOnlyList<string>? arguments = null)
    {
        CommandName = commandName;
        Flags = flags;
        Output = output;
        ErrorOutput = errorOutput;
        WorkingDirectory = workingDirectory;
        Arguments = arguments ?? Array.Empty<string>();
    }


    public string CommandName { get; }
    public IReadOnlyList<IFlagObject> Flags { get; }
    public ITextWriter Output { get; }
    public ITextWriter ErrorOutput { get; }
    public string WorkingDirectory { get; }
    public IReadOnlyList<string> Arguments { get; }
}