using CLX.Core.Commands;
using CLX.Core.Pipeline.Helpers;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CLX.Core.Pipeline;

/// <summary>
/// <summary> Binds positional arguments from <see cref="ICommandContext"/> to properties on an <see cref="ICommand"/>
/// that are decorated with <see cref="FlagValueAttribute"/>. </summary>
internal class FlagValueBinder
{
    static readonly Dictionary<string, Regex> _regexCache = new(StringComparer.Ordinal);

    string _errorMessage = string.Empty;

    /// <summary> Gets the most recent error message. </summary>
    public string ErrorMessage => _errorMessage;

    /// <summary> Attempts to bind positional arguments from the command context to properties on the 
    /// command that are decorated with the FlagValueAttribute. </summary>
    /// <param name="command">The command instance to bind arguments to</param>
    /// <param name="context">The command context containing the arguments</param>
    /// <returns>True if binding succeeded, false otherwise with error message set</returns>
    public bool TryBind(ICommand command, ICommandContext context)
    {
        // Clear any previous error message
        _errorMessage = string.Empty;
        
        // Get all properties decorated with FlagValueAttribute
        var pairs = GetFlagValueProperties(command);

        if (pairs.Count == 0)
            return true; // Nothing to bind

        // Sort by index to ensure correct order
        pairs.Sort((a, b) => a.Attr.Index.CompareTo(b.Attr.Index));

        // Partition the arguments into buckets for each property
        if (!PartitionFlagValues(pairs.Select(p => p.Attr).ToList(), context.Arguments, out var buckets))
            return false;

        // Bind each property with its corresponding values
        foreach (var (attr, prop) in pairs)
        {
            var values = buckets[attr.Index];
            var propType = prop.PropertyType;

            try 
            {
                prop.SetValue(command, GetPropValue(propType, values));
            }   
            catch (Exception ex)
            {
                var name = string.IsNullOrWhiteSpace(attr.Name) ? $"arg{attr.Index}" : attr.Name;
                _errorMessage = $"Failed to bind argument '<{name}>' to property '{prop.Name}': {ex.Message}";
                return false;
            }
        }

        return true;
    }

    /// <summary> Partitions the input values into buckets corresponding to each argument attribute. </summary>
    /// <param name="attrs">List of argument attributes defining the expected arguments</param>
    /// <param name="values">List of input values to partition.</param>
    /// <param name="buckets">Output list of value buckets, one for each attribute.</param>
    /// <returns>True if partitioning succeeded, false otherwise.</returns>
    bool PartitionFlagValues(
        IReadOnlyList<FlagValueAttribute> attrs,
        IReadOnlyList<string> values,
        out List<IReadOnlyList<string>> buckets)
    {
        buckets = new List<IReadOnlyList<string>>(attrs.Count);

        // Only last can be variadic
        for (var i = 0; i < attrs.Count - 1; i++)
            if (attrs[i].MaxValues == int.MaxValue)
            {
                _errorMessage = "Only the last argument may be variadic";
                return false;
            }

        var remainingValues = new Queue<string>(values);
        for (var i = 0; i < attrs.Count; i++)
        {
            // Get the current attribute and determine if it's the last one
            var attr = attrs[i];
            var isLastAttr = i == attrs.Count - 1;
            
            // Calculate the minimum number of values needed for this attribute
            // If the attribute is required, ensure at least 1 value is assigned
            // Otherwise, use the specified minimum

            // Calculate the minimum required values for this attribute
            // If required, ensure at least 1 value (or MinValues, whichever is greater)
            var minParams = Math.Max(attr.MinValues, attr.IsRequired ? Math.Max(1, attr.MinValues) : attr.MinValues);
            var maxParams = attr.MaxValues;

            // Calculate how many values are needed for all remaining attributes
            var minForRest = 0;
            for (var r = i + 1; r < attrs.Count; r++)
            {
                var next = attrs[r];
                minForRest += Math.Max(next.MinValues, next.IsRequired ? Math.Max(1, next.MinValues) : next.MinValues);
            }

            // Calculate how many values we can use for this attribute
            // We need to ensure we leave enough values for all remaining attributes
            var canUse = Math.Max(0, remainingValues.Count - minForRest);
            
            // Determine how many values to take:
            // - If this is the last attribute and it's variadic, take all remaining values
            // - Otherwise, take the minimum of maxParams and the number we can use
            var take = isLastAttr && maxParams == int.MaxValue ? remainingValues.Count : Math.Min(maxParams, canUse);

            // Check if we have enough values to satisfy the minimum requirement
            if (take < minParams)
            {
                var name = string.IsNullOrWhiteSpace(attr.Name) ? $"arg{attr.Index}" : attr.Name;
                _errorMessage = $"Missing required argument '<{name}>'";
                return false;
            }

            var chunk = new List<string>(take);
            
            for (var c = 0; c < take; c++)
                chunk.Add(remainingValues.Dequeue());

            // If no regex pattern is specified, add the chunk to buckets and continue
            if (attr.ValueRegexPattern == null)
            {
                buckets.Add(chunk.AsReadOnly());
                continue;
            }

            // Get the regex for the pattern
            var regex = GetCachedRegex(attr.ValueRegexPattern);
            
            foreach (var s in chunk)
                if (!regex.IsMatch(s))
                {
                    var name = string.IsNullOrWhiteSpace(attr.Name) ? $"arg{attr.Index}" : attr.Name;
                    _errorMessage = $"Invalid value '{s}' for argument '<{name}>' - must match pattern '{attr.ValueRegexPattern}'";
                    return false;
                }
            

            buckets.Add(chunk.AsReadOnly());
        }

        // If there are any remaining values after processing all attributes,
        // we have too many arguments
        if (remainingValues.Count > 0)
        {
            _errorMessage = "Too many positional arguments";
            return false;
        }

        // All values have been successfully partitioned
        return true;
    }

    #region Get Property Value
    /// <summary> Converts a list of string values to a property value of the specified type. </summary>
    /// <returns>The converted property value, or null if conversion failed.</returns>
    object? GetPropValue(Type propType, IReadOnlyList<string> values)
    {
        if (propType.IsArray)
            return TryGetArrayPropVal(values, propType, out var propValue) ? propValue : null;
        else if (IsGenericList(propType, out var itemType))
            return TryGetListPropVal(itemType, values, out var propValue) ? propValue : null;
        else if (values.Count == 0)
            return null;
        else
            return TryConvertValue(values[0], propType, out var propValue) ? propValue : null;
    }

    /// <summary> Attempts to convert a list of string values to a generic List of the specified item type. </summary>
    bool TryGetListPropVal(Type? itemType, IReadOnlyList<string> values, out object? propValue)
    {
        propValue = null;


        if (itemType == null)
            return false;

        // Convert all values to the item type
        if (!TryConvertValues(values, itemType, out var convertedList)) 
            return false;

        var listType = typeof(List<>).MakeGenericType(itemType);
        var list = (IList)(Activator.CreateInstance(listType) 
            ?? throw new NullReferenceException("Failed to create list instance"));

        foreach (var o in convertedList) 
            list.Add(o);

        propValue = list;
        return true;
    }


    /// <summary> Attempts to convert a list of string values to an array of the specified element type. </summary>/// 
    bool TryGetArrayPropVal(IReadOnlyList<string> values, Type propType, out object? propValue)
    {
        propValue = null;

        var elemType = propType.GetElementType()!;
        

        if (!TryConvertValues(values, elemType, out var convertedList))
            return false;

        var array = Array.CreateInstance(elemType, convertedList.Count);

        for (var j = 0; j < convertedList.Count; j++)
            array.SetValue(convertedList[j], j);

        propValue = array;
        return true;
    }
    #endregion

    #region Convert Values
    bool TryConvertValue(string value, Type targetType, out object? result)
    {
        try
        {
            var method = typeof(TypeConversion).GetMethod(nameof(TypeConversion.TryConvert))!;
            var generic = method.MakeGenericMethod(targetType);
            var parameters = new object?[] { value, null, null };
            var ok = (bool)generic.Invoke(null, parameters)!;

            _errorMessage = parameters[2] as string ?? string.Empty;
            result = parameters[1];

            return ok;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Conversion error for value '{value}' to {targetType.Name}: {ex.Message}";
            result = null;
            return false;
        }
    }

    bool TryConvertValues(IReadOnlyList<string> values, Type elemType, out IReadOnlyList<object> result)
    {
        var convertedList = new List<object>();

        for (int i = 0; i < values.Count; i++)
        {
            var value = values[i];
            if(TryConvertValue(value, elemType, out var item))
            {
                convertedList.Add(item ?? throw new NullReferenceException($"Conversion of '{value}' to {elemType.Name} returned null"));
                continue;
            }

            // Add index information to error message
            _errorMessage = $"Error at value {i+1}: {_errorMessage}";
            result = [];
            return false;
        }

        result = convertedList.AsReadOnly();
        return true;
    } 
    #endregion

    #region Helper Methods
    /// <summary>
    /// Gets all properties on the command that are decorated with FlagValueAttribute
    /// </summary>
    /// <param name="command">The command instance to inspect</param>
    /// <returns>A list of tuples containing the attribute and property info</returns>
    static List<(FlagValueAttribute Attr, PropertyInfo Prop)> GetFlagValueProperties(ICommand command)
    {
        var list = new List<(FlagValueAttribute, PropertyInfo)>();
        var type = command.GetType();

        // Get all properties, including non-public ones
        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var argAttrs = prop.GetCustomAttributes(typeof(FlagValueAttribute), inherit: true)
                .OfType<FlagValueAttribute>()
                .FirstOrDefault();

            if (argAttrs != null) 
                list.Add((argAttrs, prop));
        }

        return list;
    }

    static bool IsGenericList(Type t, out Type? itemType)
    {
        itemType = null;

        if (!t.IsGenericType) 
            return false;

        var genericTypeDef = t.GetGenericTypeDefinition();

        if (genericTypeDef != typeof(List<>)) 
            return false;

        itemType = t.GetGenericArguments().First();
        return true;
    }
    
    private Regex GetCachedRegex(string pattern)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }
        
        Regex? regex = null;
        
        lock (_regexCache)
        {
            if (_regexCache.TryGetValue(pattern, out regex))
            {
                return regex;
            }
            
            regex = new Regex(pattern, RegexOptions.Compiled);
            _regexCache[pattern] = regex;
        }
        
        return regex;
    }
    #endregion
}


