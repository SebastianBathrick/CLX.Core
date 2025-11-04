using CLX.Core.Nodes;

namespace CLX.Core.Lexing;

/// <summary> Lexes raw command-line arguments into trees of <see cref="INode"/>s representing
/// commands, flags, and values. </summary>
/// <remarks> Recognizes command names from the provided set and validates flag syntax (single-dash
/// short names and double-dash long names with kebab-case). </remarks>
partial class Lexer : ILexer
{
    public bool TryCreateCommandNodes(string[] args, HashSet<string> cmdNames, out IReadOnlyList<CommandNode> nodes, out string errorArg)
    {
        var argIndex = 0;
        var cmdList = new List<CommandNode>();

        while (GetCommandNode(args, ref argIndex, cmdNames) is CommandNode cmd)
            cmdList.Add(cmd);

        if (argIndex < args.Length - 1)
        {
            nodes = [];
            errorArg = args[argIndex];
            return false;
        }

        nodes = cmdList.AsReadOnly();
        errorArg = string.Empty;
        return true;
    }

    static CommandNode? GetCommandNode(string[] args, ref int argIndex, HashSet<string> cmdNames)
    {
        if (argIndex >= args.Length)
            return null;

        var cmdName = args[argIndex].Trim();

        if (!IsValidAlias(cmdName) || !cmdNames.Contains(cmdName))
            return null;

        argIndex++;

        var flagNodes = new List<FlagNode>();

        while (GetFlagNode(args, ref argIndex, cmdNames) is FlagNode flagNode)
            flagNodes.Add(flagNode);

        return new CommandNode(cmdName, flagNodes.AsReadOnly());
    }

    static FlagNode? GetFlagNode(string[] args, ref int argIndex, HashSet<string> cmdNames)
    {
        if (argIndex >= args.Length)
            return null;

        var flagName = args[argIndex].Trim();

        if (!IsValidFlagSyntax(flagName))
            return null;

        argIndex++;

        var valuesList = new List<ValueNode>();

        while (GetFlagValueNode(args, ref argIndex, cmdNames) is ValueNode valueNode)
            valuesList.Add(valueNode);

        // Remove flag dashes so during parsing the name can be used to identify the correct flag attribute
        var normalizedName = GetNormalizedFlagName(flagName);

        return new FlagNode(normalizedName, valuesList.AsReadOnly());
    }

    static ValueNode? GetFlagValueNode(string[] args, ref int argIndex, HashSet<string> cmdNames)
    {
        if (argIndex >= args.Length)
            return null;

        var argVal = args[argIndex].Trim();

        if (cmdNames.Contains(argVal) || IsValidFlagSyntax(argVal))
            return null;

        argIndex++;

        return new ValueNode(argVal);
    }

    static bool IsValidFlagSyntax(string arg)
    {
        if (string.IsNullOrWhiteSpace(arg) || arg.Length < MIN_FLAG_LEN)
            return false;

        var flagNameIndex = 0;

        // All flags start with at least one dash
        if (arg[flagNameIndex++] != FLAG_NAME_PREFIX)
            return false;

        // The full name will use two '-' and the alternative alias will use just one
        if (arg[flagNameIndex] == FLAG_NAME_PREFIX)
            flagNameIndex++;
        // Alternative aliases can only contain one alphabetic character after the dash
        else if (arg.Length > FLAG_ALT_NAME_MAX_LEN)
            return false;

        // Evaluate the alias (ex. "--flag" alias would be "flag", hence the index argument)
        return IsValidAlias(arg, flagNameIndex);
    }

    static bool IsValidAlias(string arg, int indexOffset = 0)
    {
        // If the flag ends with a dash (e.g. "--", "--my-flag-", ect.)
        if (arg.Last() == FLAG_NAME_PREFIX || arg.Last() == ALIAS_KEBAB)
            return false;

        char prevChar = default;

        // Each character must be alphabetic or a dash      
        foreach (var c in arg.Substring(indexOffset))
        {
            if (c < 'a' || c > 'z')
                // For a dash to be valid it can't be the first char or have any neighboring dash(es)
                if (prevChar == default || prevChar == ALIAS_KEBAB || c != ALIAS_KEBAB)
                    return false;

            prevChar = c;
        }

        return true;
    }

    static string GetNormalizedFlagName(string flagName) => 
        // The full name will use two '-' and the alternative alias will use just one
        flagName.StartsWith($"{FLAG_NAME_PREFIX}{FLAG_NAME_PREFIX}") ? 
        flagName.Substring(FLAG_NORMALIZED_INDEX) :  flagName.Substring(FLAG_ALT_NAME_INDEX);
}
partial class Lexer
{
    const char FLAG_NAME_PREFIX = '-';
    const char ALIAS_KEBAB = '-';
    const int MIN_FLAG_LEN = 2; // e.g. "-f"
    const int FLAG_ALT_NAME_MAX_LEN = 3; // e.g. "-f"
    const int FLAG_NORMALIZED_INDEX = 2;
    const int FLAG_ALT_NAME_INDEX = 1;
}
