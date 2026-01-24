using System;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Aion.Util.Templates;

public class EnvironmentVariableObject : ScriptObject
{
    public static readonly ScriptObject Default = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Environment"] = new EnvironmentVariableObject()
    };

    // meta: Scriban calls this for BOTH Environment.Name and Environment["Name"]
    public override bool TryGetValue(TemplateContext context, SourceSpan span, string member, out object value)
    {
        // core: Fetch from OS
        var temp = Environment.GetEnvironmentVariable(member);

        if (temp is null)
        {
            throw new ScriptRuntimeException(span, $"Environment variable '{member}' not defined.");
        }

        if (string.IsNullOrEmpty(temp))
        {
            throw new ScriptRuntimeException(span, $"Environment variable '{member}' is empty.");
        }

        value = temp;
        return true;
    }

    // meta: Optional but recommended to prevent users from accidentally
    // trying to overwrite Environment vars inside the template.
    public override bool CanWrite(string member) => false;
}