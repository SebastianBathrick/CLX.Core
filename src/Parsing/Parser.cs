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
                if (!IsValidFlag(flagNode, flagAttrs, out var flagAttr, out errorMessage))
                    return false;

                if (flagAttr == null)
                    return false;

                var flagValues = GetFlagValues(flagNode.ValueNodes);

                // We retrieved the attribute to retrieve both the name & alternate name
                var flagObj = new FlagObject(flagAttr.Name, flagAttr.AlternateName, flagValues);

                flagObjList.Add(flagObj);
            }

            var reqFlagAttrs = flagAttrs?.Where(attr => attr.IsRequired).ToList() ?? [];

            foreach(var attr in reqFlagAttrs)
                if(!flagObjList.Any(obj => obj.Name == attr.Name || obj.AlternateName == attr.Name))
                {
                    errorMessage = $"Flag {attr.Name} is required";
                    return false;
                }

            var context = new CommandContext(
                cmdNode.Name, 
                flagObjList.AsReadOnly(), 
                cmd.Output, 
                cmd.ErrorOutput, 
                cmd.WorkingDirectory);
    
            contextList.Add(context);
        }

        contexts = contextList.AsReadOnly();
        return true;
    }

    static bool IsValidFlag(
        FlagNode flagNode, 
        FlagAttributes? flagAttrs, 
        out FlagAttribute? flagAttr, 
        out string errorMessage
        )
    {
        flagAttr = null;
        var givenName = flagNode.GivenName;

        if (flagAttrs == null)
        {
            errorMessage = $"Flag {givenName} is not supported for this command";
            return false;
        }
        
        flagAttr = GetFlagAttribute(givenName, flagAttrs);

        if (flagAttr == null)
        {
            errorMessage = $"Flag {givenName} is not a valid flag";
            return false;
        }

        if (flagNode.ValueNodes.Count < flagAttr.MinValues || flagNode.ValueNodes.Count > flagAttr.MaxValues)
        {
            errorMessage = $"Flag {givenName} must have {flagAttr.MinValues} to {flagAttr.MaxValues} values";
            return false;
        }

        if (!IsValidValues(flagNode.ValueNodes, flagAttr, out errorMessage))
            return false;

        return true;
    }
    
    static bool IsValidValues(IReadOnlyList<ValueNode> valueNodes, FlagAttribute flagAttr, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (flagAttr.ValueRegexPattern == null)
            return true;

        var regex = new Regex(flagAttr.ValueRegexPattern);

        foreach (var value in valueNodes)
        {
            // This should never happen, because the lexer should have already validated the nodes
            if (value is not ValueNode valueNode)
                continue;

            if (regex.IsMatch(valueNode.Value))
                continue;

            errorMessage = $"Flag {flagAttr.Name} value {valueNode.Value} does not match regex {flagAttr.ValueRegexPattern}";
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

    static FlagAttributes? GetFlagAttributes(ICommand cmd)
    {
        var type = cmd.GetType();
        var flagAttrsList = new List<FlagAttribute>();

        // Collect FlagAttribute declared on properties (public/non-public)
        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (var attr in prop.GetCustomAttributes(typeof(FlagAttribute), inherit: true))
                if (attr is FlagAttribute flagAttr)
                    flagAttrsList.Add(flagAttr);
        }

        return flagAttrsList.Count > 0 ? flagAttrsList.AsReadOnly() : null;
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
}