using CLX.Core.Nodes;
using CLX.Core.Commands;
using System.Text.RegularExpressions;
using System.Reflection;

namespace CLX.Core.Parsing;

using ValidCommands = IReadOnlyDictionary<string, ICommand>;
using Contexts = IReadOnlyList<ICommandContext>;
using FlagAttributes = IReadOnlyList<FlagAttribute>;

/// <summary> Converts lexical nodes into executable command contexts for the runtime. </summary>
/// <remarks> Implements a Try-style API and helper methods to validate flags and values without
/// throwing, returning descriptive error messages on failure. </remarks>
partial class Parser : IParser
{
    readonly Dictionary<Type, FlagAttributes> _flagAttrsCache = new();
    readonly Dictionary<string, Regex> _regexCache = new();

    public bool TryCreateCommandContexts(IReadOnlyList<CommandNode> cmdNodes, ValidCommands cmds, out Contexts contexts, out string errorMessage)
    {
        contexts = [];
        errorMessage = string.Empty;

        var contextList = new List<ICommandContext>();

        foreach (var cmdNode in cmdNodes)
        {
            // These cases should never happen, because the lexer should have already validated the nodes
            if (!cmds.ContainsKey(cmdNode.Name))
                continue;

            var cmd = cmds[cmdNode.Name];
            var flagAttrs = GetFlagAttributes(cmd);
            var flagObjList = new List<IFlagObject>();

            foreach (var flagNode in cmdNode.FlagNodes)
            {
                /* If flag is valid then get its attribute & create flag object for the user's 
                ICommand.Execute method to use */

                // Valid flags have the correct number of values & values that match any required regex 
                if (!IsValidFlag(cmdNode.Name, flagNode, flagAttrs, out var flagAttr, out errorMessage))
                    return false;

                if (flagAttr == null)
                    return false;

                var flagValues = GetFlagValues(flagNode.ValueNodes);

                // We retrieved the attribute to retrieve both the name & alternate name
                var flagObj = new FlagObject(flagAttr.Name, flagAttr.AlternateName, flagValues);

                flagObjList.Add(flagObj);
            }

            var reqFlagAttrs = flagAttrs?.Where(attr => attr.IsRequired).ToList() ?? [];

            foreach (var attr in reqFlagAttrs)
                if (!flagObjList.Any(obj => obj.Name == attr.Name || obj.AlternateName == attr.Name))
                {
                    errorMessage = $"Missing required flag '--{attr.Name}' for '{cmdNode.Name}'";
                    return false;
                }

            var context = new CommandContext(
                cmdNode.Name, 
                flagObjList.AsReadOnly(), 
                cmd.Output ?? NullTextWriter.Instance, 
                cmd.ErrorOutput ?? NullTextWriter.Instance, 
                cmd.WorkingDirectory);
    
            contextList.Add(context);
        }

        contexts = contextList.AsReadOnly();
        return true;
    }

    bool IsValidFlag(
        string commandName,
        FlagNode flagNode,
        FlagAttributes? flagAttrs,
        out FlagAttribute? flagAttr,
        out string errorMessage)
    {
        flagAttr = null;
        var givenName = flagNode.GivenName;

        if (flagAttrs == null)
        {
            errorMessage = $"Command '{commandName}' does not accept flags: '{FormatFlagForDisplay(givenName)}'";
            return false;
        }
        
        flagAttr = GetFlagAttribute(givenName, flagAttrs);

        if (flagAttr == null)
        {
            errorMessage = $"Unknown flag '{FormatFlagForDisplay(givenName)}' for '{commandName}'";
            return false;
        }

        if (flagNode.ValueNodes.Count < flagAttr.MinValues || flagNode.ValueNodes.Count > flagAttr.MaxValues)
        {
            errorMessage = $"Flag '--{flagAttr.Name}' for '{commandName}' expects {flagAttr.MinValues}..{flagAttr.MaxValues} values";
            return false;
        }

        if (!IsValidValues(flagNode.ValueNodes, flagAttr, out errorMessage))
            return false;

        return true;
    }
    
    bool IsValidValues(IReadOnlyList<ValueNode> valueNodes, FlagAttribute flagAttr, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (flagAttr.ValueRegexPattern == null)
            return true;

        var regex = GetOrCreateRegex(flagAttr.ValueRegexPattern);

        foreach (var value in valueNodes)
        {
            // This should never happen, because the lexer should have already validated the nodes
            if (value is not ValueNode valueNode)
                continue;

            if (regex.IsMatch(valueNode.Value))
                continue;

            errorMessage = $"Invalid value '{valueNode.Value}' for '--{flagAttr.Name}': must match {flagAttr.ValueRegexPattern}";
            return false;
        }

        return true;
    }

    static FlagAttribute? GetFlagAttribute(string givenName, FlagAttributes? flagAttrs)
    {
        if (flagAttrs == null)
            return null;

        foreach (var attr in flagAttrs)
            if (attr.Name == givenName || attr.AlternateName == givenName)
                return attr;

        return null;
    }

    FlagAttributes? GetFlagAttributes(ICommand cmd)
    {
        var type = cmd.GetType();

        if (_flagAttrsCache.TryGetValue(type, out var cached))
            return cached;

        var flagAttrsList = new List<FlagAttribute>();

        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (var attr in prop.GetCustomAttributes(typeof(FlagAttribute), inherit: true))
                if (attr is FlagAttribute flagAttr)
                    flagAttrsList.Add(flagAttr);
        }

        var result = flagAttrsList.Count > 0 ? flagAttrsList.AsReadOnly() : null;
        if (result != null)
            _flagAttrsCache[type] = result;
        return result;
    }

    static IReadOnlyList<string> GetFlagValues(IReadOnlyList<ValueNode> valueNodes)
    {
        if (valueNodes.Count == 0)
            return [];

        var values = new List<string>();

        foreach (var valueNode in valueNodes)
            values.Add(valueNode.Value);

        return values.AsReadOnly();
    }

    static string FormatFlagForDisplay(string givenName) =>
        givenName.Length == 1 ? $"-{givenName}" : $"--{givenName}";

    Regex GetOrCreateRegex(string pattern)
    {
        if (_regexCache.TryGetValue(pattern, out var rx))
            return rx;
        rx = new Regex(pattern, RegexOptions.Compiled);
        _regexCache[pattern] = rx;
        return rx;
    }
}