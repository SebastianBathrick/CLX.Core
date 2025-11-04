using CLX.Core.Commands;
using CLX.Core.Nodes;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CLX.Core.Parsing;

using ArgAttributes = IReadOnlyList<ArgumentAttribute>;
using Contexts = IReadOnlyList<ICommandContext>;
using FlagAttributes = IReadOnlyList<FlagAttribute>;
using ValidCommands = IReadOnlyDictionary<string, ICommand>;

/// <summary> Converts lexical nodes into executable command contexts for the runtime. </summary>
/// <remarks> Implements a Try-style API and helper methods to validate flags and values without
/// throwing, returning descriptive error messages on failure. </remarks>
partial class Parser : IParser
{
    readonly Dictionary<Type, FlagAttributes> _flagAttrsCache = [];
    readonly Dictionary<Type, ArgAttributes> _argAttrsCache = [];
    readonly Dictionary<string, Regex> _regexCache = [];

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
            var argAttrs = GetArgumentAttributes(cmd);
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

            // Validate positional arguments
            if (!IsValidArguments(cmdNode.Name, cmdNode.PositionalNodes, argAttrs, out errorMessage))
                return false;

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
                cmd.WorkingDirectory,
                GetPositionalValues(cmdNode.PositionalNodes));

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

        // Command does not accept flags
        if (flagAttrs == null)
        { errorMessage = $"Command '{commandName}' does not accept flags: '{FormatFlagForDisplay(givenName)}'"; return false; }

        flagAttr = GetFlagAttribute(givenName, flagAttrs);

        // Flag not declared
        if (flagAttr == null)
        { errorMessage = $"Unknown flag '{FormatFlagForDisplay(givenName)}' for '{commandName}'"; return false; }

        // Arity mismatch
        if (flagNode.ValueNodes.Count < flagAttr.MinValues || flagNode.ValueNodes.Count > flagAttr.MaxValues)
        { errorMessage = $"Flag '--{flagAttr.Name}' for '{commandName}' expects {flagAttr.MinValues}..{flagAttr.MaxValues} values"; return false; }

        return IsValidValues(flagNode.ValueNodes, flagAttr, out errorMessage);
    }

    bool IsValidArguments(
        string commandName,
        IReadOnlyList<ValueNode> positionalNodes,
        ArgAttributes? argAttrs,
        out string errorMessage)
    {
        errorMessage = string.Empty;

        if (argAttrs == null || argAttrs.Count == 0)
        {
            if (positionalNodes.Count == 0) return true;
            errorMessage = $"Command '{commandName}' does not accept positional arguments: '{string.Join(" ", positionalNodes.Select(v => v.Value))}'";
            return false;
        }

        // Only the last argument may be variadic (MaxValues == int.MaxValue)
        for (var i = 0; i < argAttrs.Count - 1; i++)
            if (argAttrs[i].MaxValues == int.MaxValue)
            { errorMessage = $"Invalid argument declaration for '{commandName}': only the last argument may be variadic"; return false; }

        // Partition positional values across declared arguments while satisfying minima for remaining args
        var remaining = new Queue<string>(positionalNodes.Select(v => v.Value));
        for (var i = 0; i < argAttrs.Count; i++)
        {
            var attr = argAttrs[i];
            var isLast = i == argAttrs.Count - 1;

            var min = GetMinRequired(attr);
            var max = attr.MaxValues;

            // Ensure enough values remain for the rest (sum of minima)
            var minForRest = SumMinForRest(argAttrs, i + 1);

            var canUse = Math.Max(0, remaining.Count - minForRest);
            var take = isLast && max == int.MaxValue ? remaining.Count : Math.Min(max, canUse);

            // Ensure we meet minimum
            if (take < min)
            {
                var name = GetArgDisplayName(attr);
                errorMessage = $"Missing required argument '<{name}>' for '{commandName}'";
                return false;
            }

            // Validate regex for the values we are about to take
            if (!string.IsNullOrEmpty(attr.ValueRegexPattern))
            {
                var regex = GetOrCreateRegex(attr.ValueRegexPattern);
                foreach (var value in remaining.Take(take))
                    if (!regex.IsMatch(value))
                    { errorMessage = $"Invalid value '{value}' for argument '<{GetArgDisplayName(attr)}>': must match {attr.ValueRegexPattern}"; return false; }
            }

            // Consume the values for this argument
            for (var c = 0; c < take; c++)
                remaining.Dequeue();
        }

        // Any leftover values are too many
        if (remaining.Count > 0)
        { errorMessage = $"Too many positional arguments for '{commandName}'"; return false; }

        return true;
    }

    static int GetMinRequired(ArgumentAttribute attr)
        => Math.Max(attr.MinValues, attr.IsRequired ? Math.Max(1, attr.MinValues) : attr.MinValues);

    static int SumMinForRest(ArgAttributes attrs, int startIndex)
    {
        var total = 0;
        for (var r = startIndex; r < attrs.Count; r++)
            total += GetMinRequired(attrs[r]);
        return total;
    }

    static string GetArgDisplayName(ArgumentAttribute attr)
        => string.IsNullOrWhiteSpace(attr.Name) ? $"arg{attr.Index}" : attr.Name!;

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

    ArgAttributes? GetArgumentAttributes(ICommand cmd)
    {
        var type = cmd.GetType();

        if (_argAttrsCache.TryGetValue(type, out var cached))
            return cached;

        var list = new List<ArgumentAttribute>();
        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (var attr in prop.GetCustomAttributes(typeof(ArgumentAttribute), inherit: true))
                if (attr is ArgumentAttribute aa)
                    list.Add(aa);
        }

        if (list.Count == 0)
            return null;

        list.Sort((a, b) => a.Index.CompareTo(b.Index));
        var result = list.AsReadOnly();
        _argAttrsCache[type] = result;
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

    static IReadOnlyList<string> GetPositionalValues(IReadOnlyList<ValueNode> valueNodes)
    {
        if (valueNodes.Count == 0) return [];
        var values = new List<string>(valueNodes.Count);
        foreach (var vn in valueNodes) values.Add(vn.Value);
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