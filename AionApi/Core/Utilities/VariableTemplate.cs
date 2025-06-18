using System;
using System.Collections.Generic;
using Scriban;
using Scriban.Runtime;

namespace AionApi.Utilities;

public static class VariableTemplate
{
    public static string Render(string template, IDictionary<string, string> variables, int maxPasses = 3)
    {
        var customFunctions = new ScriptObject();
        customFunctions.Import("env", new Func<string, string>(name => Environment.ExpandEnvironmentVariables($"%{name}%")));

        var customVariables = new ScriptObject();
        customVariables.Import(variables);

        var customContext = new TemplateContext();
        customContext.PushGlobal(customFunctions);
        customContext.PushGlobal(customVariables);

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

        return Template.Parse(template).Render(customContext);
    }
}