using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Aion.Core.Flairs;

public class FindsLoggingPreset
{
    public static async Task<JsonObject> Where(string profilePath, LoggingPresetRef presetRef)
    {
        // meta: Create the path to the logging-presets-file and load it.
        var presetPath = Path.Combine(profilePath, presetRef.File);
        if (await LoggingPresetGroup.FromJson(presetPath) is { } loggingPresetGroup)
        {
            try
            {
                // core: Return the configuration.
                return loggingPresetGroup[presetRef.Name].Serilog;
            }
            catch (InvalidOperationException)
            {
                throw new LoggingPresetNotFoundException(presetRef.File, presetRef.Name);
            }
        }

        throw new FileNotFoundException($"Logging preset file '{presetPath}' not found.", fileName: presetPath);
    }
}

public class LoggingPresetNotFoundException(string presetFile, string presetName)
    : Exception($"Logging preset '{presetName}' not found in '{presetFile}'.");

