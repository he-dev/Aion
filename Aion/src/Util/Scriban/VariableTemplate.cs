using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Aion.Util.Scriban;

// https://github.com/scriban/scriban/tree/master/doc

// !! Make code for creating variable groups reusable.
public abstract class VariableGroup(string name) : IEnumerable<KeyValuePair<string, object?>>
{
    private static IEqualityComparer<string> Comparer => StringComparer.OrdinalIgnoreCase;

    public ScriptObject ToScriptObject()
    {
        var members = new ScriptObject(Comparer);
        members.Import(this.ToDictionary(Comparer));
        return new ScriptObject(Comparer) { [name] = members };
    }

    public abstract IEnumerator<KeyValuePair<string, object?>> GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

public static class VariableTemplate
{
    public static string Render(string template, IEnumerable<VariableGroup> variableGroups)
    {
        var customFunctions = new ScriptObject(StringComparer.OrdinalIgnoreCase);
        customFunctions.Import("env", EnvironmentVariables.Get);

        var customContext = new TemplateContext
        {
            // !! Make sure no missing variable goes unnoticed.
            StrictVariables = true,
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
    // !! The template engine should throw an exception when the variable is missing, or empty.
    // ?? Custom function lets us do that.
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