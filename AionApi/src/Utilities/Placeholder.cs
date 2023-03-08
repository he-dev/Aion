using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AionApi.Utilities;

public static class Placeholder
{
    public static string Resolve(string source, IDictionary<string, string> variables)
    {
        // Make sure it's case-insensitive.
        variables = variables.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        source = variables.Aggregate(source, (current, variable) => Regex.Replace(current, @"\{" + variable.Key + @"\}", variable.Value));
        if (Regex.Match(source, @"\{(?<name>[a-z_][a-z0-9_]+)\}", RegexOptions.IgnoreCase) is { Success: true } missingVariable)
        {
            throw new ArgumentException(message: $"Variable '{missingVariable.Groups["name"].Value}' not found.", paramName: nameof(variables));
        }

        return source;
    }
}