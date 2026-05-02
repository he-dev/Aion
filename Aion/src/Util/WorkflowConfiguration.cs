using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Meta.Json;
using Aion.Util.Logging;

namespace Aion.Util;

// note: This class is deserialized from workflow JSON.
public record WorkflowConfiguration
{
    // core: Make the user specify this value explicitly, so they don't activate workflows by accident.
    public bool Enabled { get; init; }

    [MustBeCron]
    public string Cron { get; init; } = null!;

    public Dictionary<string, string>? Parameters { get; init; }

    public Dictionary<string, string>? Environment { get; init; }

    public LoggingConfiguration? Logging { get; init; }

    public StepConfiguration[] Steps { get; init; } = null!;

    [JsonIgnore]
    public WorkflowPath Path { get; init; } = null!;

    public static async Task<WorkflowConfiguration> FromFile(WorkflowPath path)
    {
        if (!File.Exists(path))
        {
            // note: This is pretty unlikely, but who knows...
            throw new FileNotFoundException($"The code is trying to load a workflow template from '{path}' that doesn't exist.", fileName: path);
        }

        await using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var template = await JsonSerializer.DeserializeAsync<WorkflowConfiguration>(fileStream, new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        }) ?? throw new Exception($"The file '{path}' does not contain a workflow template. It is empty.");

        template = template with { Path = path };

        return WorkflowTemplateValidation.Validate(template);
    }

    public static async Task<WorkflowConfiguration> FromFile(string profilePath, string workflowPath)
    {
        return await FromFile(new WorkflowPath(profilePath, WorkflowName.FromPath(workflowPath)));
    }
}

public record StepConfiguration
{
    public string? Name { get; init; }

    public bool Enabled { get; init; } = true;

    [NotNullOrWhiteSpace]
    public string FileName { get; init; } = null!;

    [JsonConverter(typeof(StepArgumentConverter))]
    public IImmutableList<StepArgument>? Arguments { get; init; }

    public Dictionary<string, string>? Environment { get; init; }

    public string? WorkingDirectory { get; init; }

    public TimeSpan? Timeout { get; init; }

    public string? OnError { get; init; }

    public LoggingConfiguration? Logging { get; init; }
}

public record LoggingConfiguration
{
    public LoggingSource? Source { get; init; }

    [JsonConverter(typeof(LoggingPresetExpressionConverter))]
    public LoggingPresetExpression? Preset { get; init; }

    public JsonObject? Custom { get; init; }

    [JsonConverter(typeof(FlagsEnumConverter<LoggingTarget>))]
    public LoggingTarget? Target { get; init; }
}