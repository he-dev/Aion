using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aion.Core.Entities;

namespace Aion.Core.Services;

public static class LoggingPresetSearch
{
    public static async Task<JsonObject?> OrPreset(this JsonObject? template, LoggingPresetRepository loggingPresets)
    {
        if (template is null) return null;

        // core: Use the logger configuration that is embedded in the workflow.
        if (template.ContainsKey("WriteTo"))
        {
            return template;
        }

        // core: Use the logger configuration that is specified by the preset.
        if (template.TryGetPropertyValue("Preset", out var preset))
        {
            var presetInfo = preset.Deserialize<LoggingPreset.Info>()!;
            return await loggingPresets.Find(presetInfo.File, presetInfo.Name);
        }

        // core: Something else has been specified.
        throw new InvalidLoggingConfigurationException();
    }
}


public class InvalidLoggingConfigurationException()
    : Exception("Unknown logging configuration. Expected either 'WriteTo' property or logging preset reference.");