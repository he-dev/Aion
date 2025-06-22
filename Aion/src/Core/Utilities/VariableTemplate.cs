using System;
using System.Collections.Generic;
using Scriban;
using Scriban.Runtime;

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
        var maxPasses = 3;

        var customFunctions = new ScriptObject();
        customFunctions.Import("env", new Func<string, string>(name => Environment.ExpandEnvironmentVariables($"%{name}%")));

        var customContext = new TemplateContext();
        customContext.PushGlobal(customFunctions);
        foreach (var variableGroup in variableGroups)
        {
            customContext.PushGlobal(variableGroup.ToScriptObject());
        }

        var current = template;
        var previous = string.Empty;
        var passes = 0;

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