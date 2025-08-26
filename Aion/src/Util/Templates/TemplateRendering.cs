using System;
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Aion.Util.Json;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Aion.Util.Templates;

// https://github.com/scriban/scriban/tree/master/doc

// util: Makes creating variable groups more convenient.

public static class TemplateRendering
{
    public static string Render(this string template, IImmutableList<TemplateVariableGroup> variableGroups)
    {
        // note: Scriban's documentation recommends creating everything from scratch each time.

        var customFunctions = new ScriptObject(StringComparer.OrdinalIgnoreCase);
        customFunctions.Import("env", TemplateFunctions.GetEnvironmentVariable);

        var customContext = new TemplateContext
        {
            // core: Make sure no missing variable goes unnoticed.
            StrictVariables = true,
            // ReSharper disable once ConvertToLambdaExpression
            // hack: Scriban does not always throw exceptions when variables are missing despite the above flag. This way it does.
            TryGetMember = ((TemplateContext context, SourceSpan span, object target, string member, out object value) =>
            {
                // hack: This is a fake comment, so that ReSharper does not put everything in a single line.
                throw new ScriptRuntimeException(span, $"Variable '{member}' not found.");
            })
        };
        customContext.PushGlobal(customFunctions);
        foreach (var variableGroup in variableGroups)
        {
            customContext.PushGlobal(variableGroup.ToScriptObject());
        }

        var current = template;
        var previous = string.Empty;
        var passes = 0;

        var maxPasses = 10;
        var parserOptions = new ParserOptions()
        {
            ExpressionDepthLimit = 10
        };
        // meta: Ensure we also render nested variables but don't fall into an infinite loop.
        while (current != previous && passes < maxPasses)
        {
            previous = current;
            current = Template.Parse(current, parserOptions: parserOptions).Render(customContext);
            passes++;
        }

        // meta: Apparently it's still not fully rendered.
        if (passes >= maxPasses && current != previous)
        {
            throw new Exception($"Variable template did not stabilize after {maxPasses} passes.");
        }

        return current;
    }

    // core: Renders each path property it finds that looks like a template.
    public static JsonObject? RenderFilePaths(this JsonObject? serilog, IImmutableList<TemplateVariableGroup> variables)
    {
        if (serilog is null) return null;

        // meta: It needs to be cloned otherwise the original object will be modified, and that's a bad thing.
        serilog = serilog.Clone();

        // core: Scan sinks for the "path" property and run it through the template engine.
        if (serilog["WriteTo"] is JsonArray writeTo)
        {
            foreach (var sink in writeTo)
            {
                // meta: Make sure the path property really exists.
                if (sink is not null && sink["Name"]?.GetValue<string>() == "File" && sink["Args"] is JsonObject args && args["path"] is JsonValue path)
                {
                    var template = path.GetValue<string>();
                    args["path"] = template.Render(variables);
                }
            }
        }

        return serilog;
    }
}