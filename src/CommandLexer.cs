namespace CLX.Lexing;

class CommandLexer : ICommandLexer
{
    const char FLAG_NAME_PREFIX = '-';
    const char ALIAS_KEBAB = '-';
    const int FLAG_ALT_NAME_MAX_LEN = 2;
    const int MIN_FLAG_LEN = 2;

    public IReadOnlyList<ILexerNode> GetNodes(string[] args, HashSet<string> cmdNames, out int endArgsIndex)
    {
        var argIndex = 0;
        var cmdList = new List<ILexerNode>();

        while(GetCommandNode(args, ref argIndex, cmdNames) is ILexerNode cmd)
            cmdList.Add(cmd);

        endArgsIndex = argIndex;
        return cmdList.AsReadOnly();
    }

    static ILexerNode? GetCommandNode(string[] args, ref int argIndex, HashSet<string> cmdNames)
    {
        if (argIndex >= args.Length)
            return null;

        var cmdName = args[argIndex].Trim();

        if (!IsValidCommandSyntax(cmdName) || !cmdNames.Contains(cmdName))
            return null;

        argIndex++;

        var flagNodes = new List<ILexerNode>();

        while(GetFlagNode(args, ref argIndex, cmdNames) is ILexerNode flagNode)
            flagNodes.Add(flagNode);

        return new CommandNode(cmdName, flagNodes.AsReadOnly());
    }

    static ILexerNode? GetFlagNode(string[] args, ref int argIndex, HashSet<string> cmdNames)
    {
        if (argIndex >= args.Length)
            return null;

        var flagName = args[argIndex].Trim();

        if (!IsValidFlagSyntax(flagName))
            return null;

        argIndex++;

        var flagArgsList = new List<ILexerNode>();

        while(GetFlagArgNode(args, ref argIndex, cmdNames) is ILexerNode flagArgNode)
            flagArgsList.Add(flagArgNode);

        return new FlagNode(flagName, flagArgsList.AsReadOnly());

    }

    static ILexerNode? GetFlagArgNode(string[] args, ref int argIndex, HashSet<string> cmdNames)
    {
        if (argIndex >= args.Length)
            return null;

        var argVal = args[argIndex].Trim();

        if (cmdNames.Contains(argVal) || IsValidFlagSyntax(argVal))
            return null;

        argIndex++;
        return new FlagArgumentNode(argVal);
    }

    static bool IsValidCommandSyntax(string arg)
    {
        foreach (var c in arg)
            if (!char.IsLetter(c))
                return false;
        return true;
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

    static bool IsValidAlias(string arg, int indexOffset)
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
