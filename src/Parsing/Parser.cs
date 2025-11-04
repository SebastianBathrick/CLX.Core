using CLX.Core.Context;
using CLX.Core.Nodes;

namespace CLX.Core.Parsing;

internal partial class Parser : IParser
{
    const string INVALID_COMMAND_STRUCTURE_ERROR_MESSAGE = "Invalid command structure";

    public bool TryCreateCommandContexts(IReadOnlyList<INode> nodes, out IReadOnlyList<ICommandContext> contexts, out string errorMessage)
    {
        var contextsList = new List<ICommandContext>();

        foreach (var node in nodes)
        {
            if (node is not CommandNode cmdNode)
            {
                contexts = [];
                errorMessage = INVALID_COMMAND_STRUCTURE_ERROR_MESSAGE;
                return false;
            }

            var flags = new List<IFlagInstance>();

            foreach (var flagNodeCandidate in cmdNode.FlagNodes)
            {
                if (flagNodeCandidate is not FlagNode flagNode)
                {
                    contexts = [];
                    errorMessage = INVALID_COMMAND_STRUCTURE_ERROR_MESSAGE;
                    return false;
                }

                if (!TryParseFlag(flagNode, out var flagInstance, out errorMessage))
                {
                    contexts = [];
                    return false;
                }

                flags.Add(flagInstance);
            }

            // Use null writers and empty working directory by default
            var context = new CommandContext(
                cmdNode.Name,
                flags.AsReadOnly(),
                NullTextWriter.Instance,
                NullTextWriter.Instance,
                string.Empty
            );

            contextsList.Add(context);
        }

        contexts = contextsList.AsReadOnly();
        errorMessage = string.Empty;
        return true;
    }

    public static bool TryParseFlag(FlagNode flagNode, out IFlagInstance flagInstance, out string errorMessage)
    {
        var values = new List<string>();

        foreach (var argNodeCandidate in flagNode.FlagArgNodes)
        {
            if (argNodeCandidate is not FlagValueNode flagValueNode)
            {
                flagInstance = default!;
                errorMessage = INVALID_COMMAND_STRUCTURE_ERROR_MESSAGE;
                return false;
            }

            if (!TryParseFlagValue(flagValueNode, out var flagValue, out errorMessage))
            {
                flagInstance = default!;
                return false;
            }

            values.Add(flagValue);
        }

        flagInstance = new FlagInstance(flagNode.Name, values.AsReadOnly());
        errorMessage = string.Empty;
        return true;
    }

    public static bool TryParseFlagValue(FlagValueNode flagValueNode, out string flagValue, out string errorMessage)
    {
        // For now, any FlagValueNode is valid and its value is used directly
        flagValue = flagValueNode.Value;
        errorMessage = string.Empty;
        return true;
    }

    private sealed class CommandContext : ICommandContext
    {
        public CommandContext(
            string commandName,
            IReadOnlyList<IFlagInstance> flags,
            ITextWriter output,
            ITextWriter errorOutput,
            string workingDirectory)
        {
            CommandName = commandName;
            Flags = flags;
            Output = output;
            ErrorOutput = errorOutput;
            WorkingDirectory = workingDirectory;
        }

        public string CommandName { get; }
        public IReadOnlyList<IFlagInstance> Flags { get; }
        public ITextWriter Output { get; }
        public ITextWriter ErrorOutput { get; }
        public string WorkingDirectory { get; }
    }
}