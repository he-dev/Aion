using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Aion.Core.Logging;

public class LoggingPresetRepository(string profilePath)
{
    public async Task<JsonObject> Find(string presetFile, string presetName)
    {
        // meta: Create the path to the logging-presets-file and load it.
        var presetPath = Path.Combine(profilePath, presetFile);
        if (await LoggingPresetGroup.FromJson(presetPath) is { } loggingPresetGroup)
        {
            try
            {
                // core: Return the configuration.
                return loggingPresetGroup[presetName].Serilog;
            }
            // meta: Single will throw this, so let's translate it to something meaningful.
            catch (InvalidOperationException)
            {
                throw new LoggingPresetNotFound(presetFile, presetName);
            }
        }

        throw new FileNotFoundException($"Logging preset file '{presetPath}' not found.", fileName: presetPath);
    }
}


public class LoggingPresetNotFound(string presetFile, string presetName)
    : Exception($"Logging preset '{presetName}' not found in '{presetFile}'.");
