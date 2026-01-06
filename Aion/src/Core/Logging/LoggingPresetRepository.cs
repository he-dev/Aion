using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Aion.Core.Logging;

public class LoggingPresetRepository(string profilePath)
{
    public async Task<JsonObject?> Find(LoggingPresetExpression? expression)
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

public class LoggingPresetExpression
{
    public LoggingPresetExpression(string value)
    {
        if (Regex.Match(value, @"^((?<file>[a-z0-9\-_]+\.json):)?(?<preset>[a-z0-9\-_]+)$", RegexOptions.IgnoreCase) is { Success: true } match)
        {
            if (match.Groups["file"] is { Success: true } file)
            {
                File = file.Value;
            }

            Name = match.Groups["preset"].Value;
        }
        else
        {
            throw new ArgumentException($"Logging preset expression '{value}' is invalid.", nameof(value));
        }
    }

    public string File { get; init; } = LoggingPresetConfiguration.DefaultFileName;

    public string Name { get; init; }
}

public class LoggingPresetExpressionConverter : JsonConverter<LoggingPresetExpression>
{
    public override LoggingPresetExpression? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetString() is { } value ? new LoggingPresetExpression(value) : null;
    }
    public override void Write(Utf8JsonWriter writer, LoggingPresetExpression value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}

public class LoggingPresetNotFound(string presetFile, string presetName)
    : Exception($"Logging preset '{presetName}' not found in '{presetFile}'.");