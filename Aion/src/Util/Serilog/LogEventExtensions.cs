using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Serilog.Events;

namespace Aion.Util.Serilog;

public static class LogEventExtensions
{
    public static bool TryGetScalar<T>(this LogEvent logEvent, string name, [MaybeNullWhen(false)] out T value)
    {
        if (logEvent.Properties.TryGetValue(name, out var property) && property is ScalarValue { Value: T scalar })
        {
            value = scalar;
            return true;
        }

        value = default;
        return false;
    }

    public static bool Matches(this LogEvent logEvent, IEnumerable<KeyValuePair<string, object>> properties)
    {
        foreach (var (key, value) in properties)
        {
            // core: Check if the log event contains the property with the given key and value.
            if (!(logEvent.Properties.TryGetValue(key, out var property) && property is ScalarValue { Value: { } scalar } && scalar.Equals(value)))
            {
                return false;
            }
        }

        return true;
    }
}