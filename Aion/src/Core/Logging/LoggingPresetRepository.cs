using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Aion.Core.Logging;

public class LoggingPresetRepository(string profilePath)
{
    public const string DefaultProfilePresetName = "logging-presets.json";

    public async Task<JsonObject> Find(string presetFile, string presetName)
    {
        // meta: Create the path to the logging-presets-file and load it.
        var presetPath = Path.Combine(profilePath, presetFile);
        if (await LoggingPresetGroup.FromJson(presetPath) is { } loggingPresetGroup)
        {
            try
            {
                // core: Return the configuration.
                return loggingPresetGroup.Presets[presetName].Serilog;
            }
            // meta: Single will throw this, so let's translate it to something meaningful.
            catch (InvalidOperationException)
            {
                throw new LoggingPresetNotFound(presetFile, presetName);
            }
        }

        throw new FileNotFoundException($"Logging preset file '{presetPath}' not found.", fileName: presetPath);
    }

    public async Task<JsonObject?> Find(string? name)
    {
        if (name is null) return null;

        if (Regex.Match(name, @"^(?<file>[a-z0-9\-_]+\.json):(?<preset>[a-z0-9\-_]+)$", RegexOptions.IgnoreCase) is { Success: true } match)
        {
            return await Find(match.Groups["file"].Value, match.Groups["preset"].Value);
        }

        return await Find(DefaultProfilePresetName, name);
    }
}


public class LoggingPresetNotFound(string presetFile, string presetName)
    : Exception($"Logging preset '{presetName}' not found in '{presetFile}'.");
