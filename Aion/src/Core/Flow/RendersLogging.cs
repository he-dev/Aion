using System;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aion.Core.Data;
using Aion.Util.Flow.Scriban;
using Aion.Util.Json;

namespace Aion.Core.Flow;

// core: Use this type for all logging-templates, so you don't forget to render them.
public static class RendersLogging
{
    public static async Task<JsonObject?> From(JsonObject? template, LoggingPresetRepository loggingPresets, IImmutableList<VariableGroup> variables)
    {
        if (template is null) return null;

        // core: Use the logger configuration that is embedded in the workflow.
        if (template.ContainsKey("WriteTo"))
        {
            return RenderFilePaths(template, variables);
        }

        if (await TryGetLoggingPreset(template, loggingPresets, variables) is { } preset)
        {
            return RenderFilePaths(preset, variables);
        }

        // core: Something else has been specified.
        throw new InvalidLoggingConfigurationException();
    }

    private static async Task<JsonObject?> TryGetLoggingPreset(JsonObject logging, LoggingPresetRepository loggingPresets, IImmutableList<VariableGroup> variables)
    {
        // core: Use the logger configuration that is specified by the preset.
        if (logging.TryGetPropertyValue("Preset", out var preset))
        {
            var presetInfo = preset.Deserialize<LoggingPreset.Info>()!;
            return await loggingPresets.Single(presetInfo.File, presetInfo.Name);
        }

        return null;
    }

    // core: Renders each path property it finds that looks like a template.
    private static JsonObject RenderFilePaths(JsonObject serilog, IImmutableList<VariableGroup> variables)
    {
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
                    args["path"] = RendersTemplates.In(template, variables);
                }
            }
        }

        return serilog;
    }
}

public class InvalidLoggingConfigurationException()
    : Exception("Unknown logging configuration. Expected either 'WriteTo' property or logging preset reference.");