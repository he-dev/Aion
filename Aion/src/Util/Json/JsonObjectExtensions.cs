using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using Aion.Util.Scriban;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Aion.Util.Json;

public static class JsonObjectExtensions
{
    // role: Renders each path property it finds that looks like a template.
    public static JsonObject? RenderPaths(this JsonObject? serilog, IImmutableList<VariableGroup> variables)
    {
        // core: Scan sinks for the "path" property and run it through the template engine.
        if (serilog?["WriteTo"] is JsonArray writeTo)
        {
            foreach (var sink in writeTo)
            {
                // meta: Make sure the path property really exists.
                if (sink is not null && sink["Name"]?.GetValue<string>() == "File" && sink["Args"] is JsonObject args && args["path"] is JsonValue path)
                {
                    var template = path.GetValue<string>();
                    args["path"] = VariableTemplate.Render(template, variables);
                }
            }
        }

        return serilog;
    }

    public static ILogger? ToLogger(this JsonObject? serilog)
    {
        if (serilog is null) return null;

        // meta: This node is required in the appsettings.json for Serilog to find its settings.
        serilog = new JsonObject { ["Serilog"] = serilog };

        // meta: Uses an in-memory appsettings.json from which Serilog can create its sinks.
        var json = serilog.ToJsonString();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var config = new ConfigurationBuilder().AddJsonStream(stream).Build();
        return new LoggerConfiguration().ReadFrom.Configuration(config).CreateLogger();
    }
}