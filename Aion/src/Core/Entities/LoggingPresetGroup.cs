using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Aion.Core.Entities;

public record LoggingPresetGroup
{
    // meta: The version of the logging-presets-file. Currently, not in use.
    public int Version { get; init; } = 1;

    public LoggingPreset[] Presets { get; init; } = [];

    // util: Finds the right preset.
    public LoggingPreset this[string name] => Presets.Single(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static async Task<LoggingPresetGroup?> FromJson(string path)
    {
        await using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<LoggingPresetGroup>(fileStream, new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        });
    }
}

// ReSharper disable once ClassNeverInstantiated.Global
public record LoggingPreset
{
    public string Name { get; set; } = null!;

    public JsonObject Serilog { get; set; } = null!;

    // core: Used in workflows to reference logging presets.
    public record Info
    {
        public string File { get; init; } = "logging-presets.json";
        public string Name { get; init; } = null!;

        public JsonObject ToJsonObject() => new()
        {
            [nameof(File)] = File,
            [nameof(Name)] = Name
        };
    }
}