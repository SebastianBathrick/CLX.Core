using System.Collections;
using System.Reflection;
using CLX.Core.Commands;

namespace CLX.Core.Parsing;

/// <summary>
/// Binds positional arguments from <see cref="ICommandContext"/> to properties on an <see cref="ICommand"/>
/// that are decorated with <see cref="ArgumentAttribute"/>.
/// </summary>
internal static class ArgumentBinder
{
    public static bool TryBind(ICommand command, ICommandContext context, out string errorMessage)
    {
        errorMessage = string.Empty;

        var pairs = GetArgumentProperties(command);

        if (pairs.Count == 0)
            return true; // Nothing to bind

        pairs.Sort((a, b) => a.Attr.Index.CompareTo(b.Attr.Index));

        if (!PartitionArguments(pairs.Select(p => p.Attr).ToList(), context.Arguments, out var buckets, out errorMessage))
            return false;

        for (var i = 0; i < pairs.Count; i++)
        {
            var (attr, prop) = pairs[i];
            var values = buckets[i];
            var propType = prop.PropertyType;

            try
            {
                if (propType.IsArray)
                { // Bind array
                    var elemType = propType.GetElementType()!;
                    var converted = ConvertMany(values, elemType, out errorMessage);
                    if (converted == null) return false;
                    var array = Array.CreateInstance(elemType, converted.Count);
                    for (int j = 0; j < converted.Count; j++) array.SetValue(converted[j], j);
                    prop.SetValue(command, array);
                    continue;
                }
                if (IsGenericList(propType, out var itemType))
                { // Bind List<T>
                    var converted = ConvertMany(values, itemType!, out errorMessage);
                    if (converted == null) return false;
                    var listType = typeof(List<>).MakeGenericType(itemType!);
                    var list = (IList)Activator.CreateInstance(listType)!;
                    foreach (var o in converted) list.Add(o);
                    prop.SetValue(command, list);
                    continue;
                }
                // Single value
                if (values.Count == 0) continue; // leave default
                if (!TryConvertTo(values[0], propType, out var obj, out errorMessage)) return false;
                prop.SetValue(command, obj);
            }
            catch (Exception ex)
            {
                var name = string.IsNullOrWhiteSpace(attr.Name) ? $"arg{attr.Index}" : attr.Name;
                errorMessage = $"Failed to bind argument '<{name}>': {ex.Message}";
                return false;
            }
        }

        return true;
    }

    static List<(ArgumentAttribute Attr, PropertyInfo Prop)> GetArgumentProperties(ICommand command)
    {
        var list = new List<(ArgumentAttribute, PropertyInfo)>();
        var type = command.GetType();
        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var aa = prop.GetCustomAttributes(typeof(ArgumentAttribute), inherit: true)
                         .OfType<ArgumentAttribute>()
                         .FirstOrDefault();
            if (aa != null) list.Add((aa, prop));
        }
        return list;
    }

    static bool PartitionArguments(
        IReadOnlyList<ArgumentAttribute> attrs,
        IReadOnlyList<string> values,
        out List<IReadOnlyList<string>> buckets,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        buckets = new List<IReadOnlyList<string>>(attrs.Count);

        // Only last can be variadic
        for (var i = 0; i < attrs.Count - 1; i++)
            if (attrs[i].MaxValues == int.MaxValue)
            {
                errorMessage = "Only the last argument may be variadic";
                return false;
            }

        var remaining = new Queue<string>(values);
        for (var i = 0; i < attrs.Count; i++)
        {
            var attr = attrs[i];
            var isLast = i == attrs.Count - 1;

            var min = Math.Max(attr.MinValues, attr.IsRequired ? Math.Max(1, attr.MinValues) : attr.MinValues);
            var max = attr.MaxValues;

            var minForRest = 0;
            for (var r = i + 1; r < attrs.Count; r++)
            {
                var next = attrs[r];
                minForRest += Math.Max(next.MinValues, next.IsRequired ? Math.Max(1, next.MinValues) : next.MinValues);
            }

            var canUse = Math.Max(0, remaining.Count - minForRest);
            var take = isLast && max == int.MaxValue ? remaining.Count : Math.Min(max, canUse);

            if (take < min)
            {
                var name = string.IsNullOrWhiteSpace(attr.Name) ? $"arg{attr.Index}" : attr.Name;
                errorMessage = $"Missing required argument '<{name}>'";
                return false;
            }

            var chunk = new List<string>(take);
            for (var c = 0; c < take; c++)
                chunk.Add(remaining.Dequeue());

            // Regex validation
            if (!string.IsNullOrEmpty(attr.ValueRegexPattern))
            {
                var rx = new System.Text.RegularExpressions.Regex(attr.ValueRegexPattern);
                foreach (var s in chunk)
                    if (!rx.IsMatch(s))
                    {
                        var name = string.IsNullOrWhiteSpace(attr.Name) ? $"arg{attr.Index}" : attr.Name;
                        errorMessage = $"Invalid value '{s}' for argument '<{name}>'";
                        return false;
                    }
            }

            buckets.Add(chunk.AsReadOnly());
        }

        if (remaining.Count > 0)
        {
            errorMessage = "Too many positional arguments";
            return false;
        }

        return true;
    }

    static bool IsGenericList(Type t, out Type? itemType)
    {
        itemType = null;
        if (!t.IsGenericType) return false;
        var def = t.GetGenericTypeDefinition();
        if (def != typeof(List<>)) return false;
        itemType = t.GetGenericArguments()[0];
        return true;
    }

    static bool TryConvertTo(string value, Type targetType, out object? result, out string? errorMessage)
    {
        var method = typeof(TypeConversion).GetMethod(nameof(TypeConversion.TryConvert))!;
        var generic = method.MakeGenericMethod(targetType);
        var parameters = new object?[] { value, null, null };
        var ok = (bool)generic.Invoke(null, parameters)!;
        errorMessage = parameters[2] as string;
        result = parameters[1];
        return ok;
    }

    static IReadOnlyList<object>? ConvertMany(IReadOnlyList<string> values, Type elemType, out string errorMessage)
    {
        errorMessage = string.Empty;
        var method = typeof(TypeConversion).GetMethod(nameof(TypeConversion.TryConvertMany))!;
        var generic = method.MakeGenericMethod(elemType);
        var parameters = new object?[] { values, null, null };
        var ok = (bool)generic.Invoke(null, parameters)!;
        errorMessage = parameters[2] as string ?? string.Empty;
        if (!ok) return null;
        var typedReadOnlyList = parameters[1]!; // IReadOnlyList<T>
        // Copy to object list
        var result = new List<object>();
        foreach (var item in (System.Collections.IEnumerable)typedReadOnlyList)
            result.Add(item!);
        return result.AsReadOnly();
    }
}


