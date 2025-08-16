using System;
using Scriban;
using Scriban.Parsing;
using Scriban.Syntax;

namespace Aion.Util.Services;

public static class TemplateFunctions
{
    // core: The template engine should throw an exception when the variable is missing, or empty.
    // hack: Custom function lets us do that.
    public static string GetEnvironmentVariable(TemplateContext context, SourceSpan span, string name)
    {
        return Environment.GetEnvironmentVariable(name) switch
        {
            null => throw new ScriptRuntimeException(span, $"Environment variable '{name}' not defined."),
            var value when string.IsNullOrEmpty(value) => throw new ScriptRuntimeException(span, $"Environment variable '{name}' is empty."),
            var value => value
        };

    }
}