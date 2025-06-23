using System;
using System.Collections.Generic;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Aion.Core.Utilities;

public static class ScriptObjectExtensions
{
    public static IScriptObject ImportDictionary(this IScriptObject target, IDictionary<string, object?> source)
    {
        target.Import(source);
        return target;
    }
}

public class VariableGroup(string name, IDictionary<string, object?>? values = null)
    : Dictionary<string, object?>(values ?? new Dictionary<string, object?>())
{
    public ScriptObject ToScriptObject() => new() { [name] = new ScriptObject().ImportDictionary(this) };
}

public static class VariableTemplate
{
    public static string Render(string template, IEnumerable<VariableGroup> variableGroups)
    {

        var customFunctions = new ScriptObject();
        customFunctions.Import("env", EnvironmentVariables.Get);

        var customContext = new TemplateContext
        {
            // !! Make sure no missing variable goes unnoticed.
            StrictVariables = true
        };
        customContext.PushGlobal(customFunctions);
        foreach (var variableGroup in variableGroups)
        {
            customContext.PushGlobal(variableGroup.ToScriptObject());
        }

        var current = template;
        var previous = string.Empty;
        var passes = 0;

        var maxPasses = 3;

        // !! Ensure we also render nested variables but don't fall into an infinite loop.
        while (current != previous && passes < maxPasses)
        {
            previous = current;
            current = Template.Parse(current).Render(customContext);
            passes++;
        }

        // .. Apparently it's still not fully rendered.
        if (passes >= maxPasses && current != previous)
        {
            throw new Exception($"Variable template did not stabilize after {maxPasses} passes: {current}");
        }

        return current;
    }
}

public static class EnvironmentVariables
{
    public static string Get(TemplateContext context, SourceSpan span, string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(value))
        {
            throw new ScriptRuntimeException(span, $"Environment variable '{name}' not defined.");
        }
        return value;
    }
}