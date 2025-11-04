using System.Globalization;

namespace CLX.Core.Parsing;

/// <summary>
/// Provides culture-invariant string to type conversion for primitives, enums and common types.
/// </summary>
static class TypeConversion
{
    public static bool TryConvert<T>(string input, out T value, out string? errorMessage)
    {
        var targetType = typeof(T);
        try
        {
            object? result;
            if (TryConvertCore(input, targetType, out result, out errorMessage))
            {
                value = (T)result!;
                return true;
            }

            value = default!;
            return false;
        }
        catch (Exception ex)
        {
            value = default!;
            errorMessage = ex.Message;
            return false;
        }
    }

    public static bool TryConvertMany<T>(IEnumerable<string> inputs, out IReadOnlyList<T> values, out string? errorMessage)
    {
        var list = new List<T>();
        foreach (var s in inputs)
        {
            if (!TryConvert<T>(s, out var v, out errorMessage))
            {
                values = Array.Empty<T>();
                return false;
            }
            list.Add(v);
        }
        values = list.AsReadOnly();
        errorMessage = null;
        return true;
    }

    static bool TryConvertCore(string input, Type targetType, out object? value, out string? errorMessage)
    {
        errorMessage = null;
        var t = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (t == typeof(string)) { value = input; return true; }
        if (t == typeof(bool)) { if (bool.TryParse(input, out var b)) { value = b; return true; } else { value = null; errorMessage = "Invalid boolean"; return false; } }
        if (t == typeof(int)) { if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) { value = i; return true; } else { value = null; errorMessage = "Invalid integer"; return false; } }
        if (t == typeof(long)) { if (long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) { value = i; return true; } else { value = null; errorMessage = "Invalid long"; return false; } }
        if (t == typeof(double)) { if (double.TryParse(input, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d)) { value = d; return true; } else { value = null; errorMessage = "Invalid double"; return false; } }
        if (t == typeof(float)) { if (float.TryParse(input, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var f)) { value = f; return true; } else { value = null; errorMessage = "Invalid float"; return false; } }
        if (t == typeof(decimal)) { if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) { value = d; return true; } else { value = null; errorMessage = "Invalid decimal"; return false; } }
        if (t == typeof(Guid)) { if (Guid.TryParse(input, out var g)) { value = g; return true; } else { value = null; errorMessage = "Invalid Guid"; return false; } }
        if (t == typeof(DateTime)) { if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)) { value = dt; return true; } else { value = null; errorMessage = "Invalid DateTime"; return false; } }
        if (t == typeof(TimeSpan)) { if (TimeSpan.TryParse(input, CultureInfo.InvariantCulture, out var ts)) { value = ts; return true; } else { value = null; errorMessage = "Invalid TimeSpan"; return false; } }
        if (t == typeof(Uri)) { if (Uri.TryCreate(input, UriKind.RelativeOrAbsolute, out var uri)) { value = uri; return true; } else { value = null; errorMessage = "Invalid Uri"; return false; } }

        if (t.IsEnum)
        {
            try
            {
                value = Enum.Parse(t, input, ignoreCase: true);
                return true;
            }
            catch
            {
                value = null;
                errorMessage = $"Invalid {t.Name} value";
                return false;
            }
        }

        // Fallback via Convert.ChangeType
        try
        {
            value = Convert.ChangeType(input, t, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex)
        {
            value = null;
            errorMessage = ex.Message;
            return false;
        }
    }
}


