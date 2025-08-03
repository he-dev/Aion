using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Core.Templates;

namespace Aion.Core;

public record Workflow
{
    // core: Make the user specify this value explicitly, so they don't activate workflows by accident.
    public bool IsOn { get; init; }

    public string Cron { get; init; } = null!;

    public string? TimeZoneId { get; init; }

    // util: Use the local time-zone if the request did not specify any.
    public TimeZoneInfo TimeZone =>
        string.IsNullOrEmpty(TimeZoneId)
            ? TimeZoneInfo.Local
            : TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    public Dictionary<string, object?> Args { get; init; } = new();

    public List<Step> Steps { get; init; } = [];

    [JsonPropertyName("SerilogOrPreset")]
    public LoggingTemplate? Logging { get; init; }

    public record Step
    {
        public string? Name { get; init; }

        public bool IsOn { get; init; } = true;

        public StringTemplate File { get; init; } = null!;

        [JsonPropertyName("ArgsOrString")]
        public ArgumentsTemplate Args { get; init; } = new(null, null);

        public StringTemplate? WorkingDirectory { get; init; }

        public TimeSpan Timeout { get; init; } = System.Threading.Timeout.InfiniteTimeSpan;

        [JsonPropertyName("SerilogOrPreset")]
        public LoggingTemplate? Logging { get; init; }

        public string? DependsOn { get; init; }
    }

    public static async Task<Workflow> FromFile(string path)
    {
        if (!File.Exists(path))
        {
            // note: This is pretty unlikely, but who knows...
            throw new FileNotFoundException($"Workflow '{path}' not found.", fileName: path);
        }

        await using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var workflow = await JsonSerializer.DeserializeAsync<Workflow>(fileStream, new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            Converters =
            {
                new StringTemplateConverter(),
                new ArgumentsTemplateConverter(),
                new LoggingTemplateConverter(),
            }
        });

        return workflow ?? throw new InvalidWorkflow(path);
    }
}

public class InvalidWorkflow(string path) : Exception($"File '{path}' is not a valid workflow.");