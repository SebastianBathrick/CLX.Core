using CLX.Core.Nodes;
using System;

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

        if (argIndex < args.Length)
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

        // Try to read a composite command name greedily (space-joined tokens)
        if (!TryReadCompositeCommandName(args, argIndex, cmdNames, out var compositeName, out var tokensConsumed))
            return null;

        var cmdName = compositeName;
        argIndex += tokensConsumed;

        var flagNodes = new List<FlagNode>();
        var positionalNodes = new List<ValueNode>();

        var stopOptions = false; // true after encountering "--"

        while (argIndex < args.Length)
        {
            // If next tokens begin a new composite command, stop this command's scan
            if (StartsWithCompositeCommandName(args, argIndex, cmdNames))
                break;

            var token = args[argIndex].Trim();

            // `--` stops option parsing; everything after is positional
            if (!stopOptions && token == "--") { stopOptions = true; argIndex++; continue; }

            // If options are still being parsed and this looks like a flag, parse it
            if (!stopOptions && IsValidFlagSyntax(token))
            {
                var flagNode = GetFlagNode(args, ref argIndex, cmdNames, cmdName);
                if (flagNode != null) { flagNodes.Add(flagNode); continue; }
                // If a token looked like a flag but didn't parse, treat it as positional
            }

            // Otherwise, treat as a positional value (including tokens that start with '-')
            positionalNodes.Add(new ValueNode(token));
            argIndex++;
        }

        return new CommandNode(cmdName, flagNodes.AsReadOnly(), positionalNodes.AsReadOnly());
    }

    static FlagNode? GetFlagNode(string[] args, ref int argIndex, HashSet<string> cmdNames, string currentCommandName)
    {
        if (argIndex >= args.Length)
            return null;

        var flagName = args[argIndex].Trim();

        if (!IsValidFlagSyntax(flagName))
            return null;

        argIndex++;

        var valuesList = new List<ValueNode>();

        // Help command's --for values can include tokens that look like command names
        var allowCompositeTokensAsValues = string.Equals(currentCommandName, "help", StringComparison.Ordinal);

        while (GetFlagValueNode(args, ref argIndex, cmdNames, allowCompositeTokensAsValues) is ValueNode valueNode)
            valuesList.Add(valueNode);

        // Remove flag dashes so during parsing the name can be used to identify the correct flag attribute
        var normalizedName = GetNormalizedFlagName(flagName);

        return new FlagNode(normalizedName, valuesList.AsReadOnly());
    }

    static ValueNode? GetFlagValueNode(string[] args, ref int argIndex, HashSet<string> cmdNames, bool treatCompositeAsValue)
    {
        if (argIndex >= args.Length)
            return null;

        var argVal = args[argIndex].Trim();

        // Stop if next tokens begin a (possibly composite) command name or a new flag
        if ((!treatCompositeAsValue && StartsWithCompositeCommandName(args, argIndex, cmdNames)) || IsValidFlagSyntax(argVal))
            return null;

        argIndex++;

        return new ValueNode(argVal);
    }

    static bool StartsWithCompositeCommandName(string[] args, int index, HashSet<string> cmdNames)
        => TryReadCompositeCommandName(args, index, cmdNames, out _, out var consumed) && consumed > 0;

    static bool TryReadCompositeCommandName(
        string[] args,
        int startIndex,
        HashSet<string> cmdNames,
        out string compositeName,
        out int tokensConsumed)
    {
        compositeName = string.Empty;
        tokensConsumed = 0;

        if (startIndex >= args.Length)
            return false;

        string? bestMatch = null;
        var bestCount = 0;

        // Greedily join tokens with space, stopping at first flag token
        var current = new List<string>();

        for (var i = startIndex; i < args.Length; i++)
        {
            var token = args[i].Trim();

            // A flag cannot be part of a command name
            if (IsValidFlagSyntax(token))
                break;

            // Command name parts must be valid aliases (lowercase letters and '-')
            if (!IsValidAlias(token))
                break;

            current.Add(token);
            var joined = string.Join(' ', current);

            if (cmdNames.Contains(joined))
            {
                bestMatch = joined;
                bestCount = current.Count;
            }
        }

        if (bestCount == 0 || string.IsNullOrEmpty(bestMatch))
            return false;

        compositeName = bestMatch;
        tokensConsumed = bestCount;
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

        // Long form: starts with "--" and then kebab-case alias
        if (arg[flagNameIndex] == FLAG_NAME_PREFIX)
        {
            flagNameIndex++;
            // Evaluate the alias (e.g., "--flag" alias would be "flag")
            return IsValidAlias(arg, flagNameIndex);
        }

        // Short form: "-x" must be exactly one alphabetic character
        if (arg.Length != 2)
            return false;

        var shortAlias = arg[flagNameIndex];
        return shortAlias >= 'a' && shortAlias <= 'z';
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
        flagName.Substring(FLAG_NORMALIZED_INDEX) : flagName.Substring(FLAG_ALT_NAME_INDEX);
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
