using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using Aion.Util.Scriban;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Aion.Util.Json;

public static class JsonObjectExtensions
{
    // role: We want to use the same ILogger API everywhere, so use the ILoggerFactory.
    public static ILogger? ToLogger(this JsonObject? serilog, IEnumerable<VariableGroup> variables)
    {
        // util: Let's use the null pattern to avoid nulls outside.
        if (serilog is null) return null;

        // core: Scan sinks for the "path" property and run it through the template engine.
        if (serilog["WriteTo"] is JsonArray writeTo)
        {
            foreach (var sink in writeTo)
            {
                if (sink is not null && sink["Name"]?.GetValue<string>() == "File" && sink["Args"] is JsonObject args && args["path"] is JsonValue path)
                {
                    var template = path.GetValue<string>();
                    args["path"] = VariableTemplate.Render(template, variables);
                }
            }
        }

        // core: This node is required in the appsettings.json for Serilog to find its settings.
        serilog = new JsonObject { ["Serilog"] = serilog };

        // meta: Uses an in-memory appsettings.json from which Serilog can create its sinks.
        var json = serilog.ToJsonString();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var config = new ConfigurationBuilder().AddJsonStream(stream).Build();
        return new LoggerConfiguration().ReadFrom.Configuration(config).CreateLogger();
    }

    // role: We want to use the same ILogger API everywhere, so use the ILoggerFactory.
    public static ILoggerFactory ToLoggerFactory(this ILogger logger)
    {
        // util: Wraps the Serilog's logger into Microsoft's logger
        return LoggerFactory.Create(builder => { builder.ClearProviders().AddSerilog(logger, dispose: true); });
    }

    // role: We want to use the same ILogger API everywhere, so use the ILoggerFactory.
    public static ILoggerFactory ToLoggerFactory(this JsonObject? serilog, IEnumerable<VariableGroup> variables)
    {
        // util: Let's use the null pattern to avoid nulls outside.
        if (serilog is null) return NullLoggerFactory.Instance;

        // core: Scan sinks for the "path" property and run it through the template engine.
        if (serilog["WriteTo"] is JsonArray writeTo)
        {
            foreach (var sink in writeTo)
            {
                if (sink is not null && sink["Name"]?.GetValue<string>() == "File" && sink["Args"] is JsonObject args && args["path"] is JsonValue path)
                {
                    var template = path.GetValue<string>();
                    args["path"] = VariableTemplate.Render(template, variables);
                }
            }
        }

        // core: This node is required in the appsettings.json for Serilog to find its settings.
        serilog = new JsonObject { ["Serilog"] = serilog };

        // meta: Uses an in-memory appsettings.json from which Serilog can create its sinks.
        var json = serilog.ToJsonString();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var config = new ConfigurationBuilder().AddJsonStream(stream).Build();
        var logger = new LoggerConfiguration().ReadFrom.Configuration(config).CreateLogger();

        // util: Wraps the Serilog's logger into Microsoft's logger
        return LoggerFactory.Create(builder => { builder.ClearProviders().AddSerilog(logger, dispose: true); });
    }
}