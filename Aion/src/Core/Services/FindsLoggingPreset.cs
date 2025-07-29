using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Aion.Core.Services;

public class FindsLoggingPreset
{
    public static async Task<JsonObject> Where(string profilePath, LoggingPresetInfo presetInfo)
    {
        // meta: Create the path to the logging-presets-file and load it.
        var presetPath = Path.Combine(profilePath, presetInfo.File);
        if (await LoggingPresetGroup.FromJson(presetPath) is { } loggingPresetGroup)
        {
            try
            {
                // core: Return the configuration.
                return loggingPresetGroup[presetInfo.Name].Serilog;
            }
            // meta: Single will throw this, so let's translate it to something meaningful.
            catch (InvalidOperationException)
            {
                throw new LoggingPresetNotFoundException(presetInfo.File, presetInfo.Name);
            }
        }

        throw new FileNotFoundException($"Logging preset file '{presetPath}' not found.", fileName: presetPath);
    }
}

public class LoggingPresetNotFoundException(string presetFile, string presetName)
    : Exception($"Logging preset '{presetName}' not found in '{presetFile}'.");

