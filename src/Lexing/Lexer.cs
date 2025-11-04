using CLX.Core.Nodes;

namespace CLX.Core.Lexing;

class Lexer : ILexer
{
    const char FLAG_NAME_PREFIX = '-';
    const char ALIAS_KEBAB = '-';
    const int FLAG_ALT_NAME_MAX_LEN = 2;
    const int MIN_FLAG_LEN = 2;

    public bool TryCreateCommandNodes(string[] args, HashSet<string> cmdNames, out IReadOnlyList<INode> nodes, out string errorArg)
    {
        var argIndex = 0;
        var cmdList = new List<INode>();

        while(GetCommandNode(args, ref argIndex, cmdNames) is INode cmd)
            cmdList.Add(cmd);

        if (argIndex < args.Length - 1)
        {
            nodes = Array.Empty<INode>();
            errorArg = args[argIndex];
            return false;
        }

        nodes = cmdList.AsReadOnly();
        errorArg = string.Empty;
        return true;
    }

    static INode? GetCommandNode(string[] args, ref int argIndex, HashSet<string> cmdNames)
    {
        if (argIndex >= args.Length)
            return null;

        var cmdName = args[argIndex].Trim();

        if (!IsValidAlias(cmdName) || !cmdNames.Contains(cmdName))
            return null;

        argIndex++;

        var flagNodes = new List<INode>();

        while(GetFlagNode(args, ref argIndex, cmdNames) is INode flagNode)
            flagNodes.Add(flagNode);

        return new CommandNode(cmdName, flagNodes.AsReadOnly());
    }

    static INode? GetFlagNode(string[] args, ref int argIndex, HashSet<string> cmdNames)
    {
        if (argIndex >= args.Length)
            return null;

        var flagName = args[argIndex].Trim();

        if (!IsValidFlagSyntax(flagName))
            return null;

        argIndex++;

        var flagArgsList = new List<INode>();

        while(GetFlagArgNode(args, ref argIndex, cmdNames) is INode flagArgNode)
            flagArgsList.Add(flagArgNode);

        return new FlagNode(flagName, flagArgsList.AsReadOnly());

    }

    static INode? GetFlagArgNode(string[] args, ref int argIndex, HashSet<string> cmdNames)
    {
        if (argIndex >= args.Length)
            return null;

        var argVal = args[argIndex].Trim();

        if (cmdNames.Contains(argVal) || IsValidFlagSyntax(argVal))
            return null;

        argIndex++;

        return new FlagValueNode(argVal);
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
}
