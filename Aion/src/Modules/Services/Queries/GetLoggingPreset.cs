using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aion.Modules.Logging;

namespace Aion.Modules.Services.Queries;

public class GetLoggingPreset(string profilePath)
{
    public async Task<JsonObject?> Where(LoggingConfiguration loggingConfiguration)
    {
        // core: Determine the logging source and return the configuration.
        return loggingConfiguration.Source switch
        {
            LoggingSource.Auto => loggingConfiguration.Custom ?? await Where(loggingConfiguration.Preset),
            LoggingSource.Preset => await Where(loggingConfiguration.Preset) ?? throw new InvalidOperationException("Preset logging is missing."),
            LoggingSource.Custom => loggingConfiguration.Custom ?? throw new InvalidOperationException("Custom logging is missing."),
            _ => null
        };
    }

    // core: Load the logging preset from the file.
    private async Task<JsonObject?> Where(LoggingPresetExpression? expression)
    {
        if (expression is null) return null;

        // meta: Create the path to the logging-presets-file and load it.
        var presetPath = Path.Combine(profilePath, expression.File);
        if (await LoggingPresetConfiguration.FromJson(presetPath) is { } loggingPresetGroup)
        {
            try
            {
                // core: Return the configuration.
                return loggingPresetGroup.Presets[expression.Name].Serilog;
            }
            // meta: Single will throw this, so let's translate it to something meaningful.
            catch (InvalidOperationException)
            {
                throw new LoggingPresetNotFound(expression.File, expression.Name);
            }
        }

        throw new FileNotFoundException($"Logging preset file '{presetPath}' not found.", fileName: presetPath);
    }
}

public class LoggingPresetNotFound(string presetFile, string presetName)
    : Exception($"Logging preset '{presetName}' not found in '{presetFile}'.");