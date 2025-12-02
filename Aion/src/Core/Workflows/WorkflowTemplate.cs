using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Aion.Core.Logging;

namespace Aion.Core.Workflows;

public record WorkflowTemplate
{
    // core: Make the user specify this value explicitly, so they don't activate workflows by accident.
    public bool Enabled { get; init; }

    [Cron]
    public string Cron { get; init; } = null!;

    public Dictionary<string, string> Variables { get; init; } = new();

    public Dictionary<string, string> Environment { get; init; } = new();

    //[SerilogOrPreset]
    public LoggingInfo Logging { get; init; } = new();

    public StepTemplate[] Steps { get; init; } = null!;

    public record StepTemplate
    {
        public string? Name { get; init; }

        public bool Enabled { get; init; } = true;

        [NotNullOrWhiteSpace]
        public string FileName { get; init; } = null!;

        public string? Arguments { get; init; }

        public Dictionary<string, string> Environment { get; init; } = new();

        public string? WorkingDirectory { get; init; }

        public TimeSpan? Timeout { get; init; }

        //[SerilogOrPreset]
        public LoggingInfo Logging { get; init; } = new();

        public string? DependsOn { get; init; }
    }

    public static async Task<WorkflowTemplate> FromFile(string path)
    {
        if (!File.Exists(path))
        {
            // note: This is pretty unlikely, but who knows...
            throw new FileNotFoundException($"Workflow template '{path}' not found.", fileName: path);
        }

        await using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var template = await JsonSerializer.DeserializeAsync<WorkflowTemplate>(fileStream, new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
        }) ?? throw new InvalidWorkflowTemplate(path);

        return WorkflowTemplateValidation.Validate(template);
    }
}

public class InvalidWorkflowTemplate(string path) : Exception($"File '{path}' is not a valid workflow template.");

public enum LoggingSource
{
    None,
    Auto,
    Preset,
    Custom,
}

public record LoggingInfo
{
    public LoggingSource Source { get; init; } = LoggingSource.Auto;
    public string? Preset { get; init; }
    public JsonObject? Custom { get; init; }

    public async Task<JsonObject?> Get(LoggingPresetRepository loggingPresets)
    {
        return Source switch
        {
            LoggingSource.Auto => Custom ?? await loggingPresets.Find(Preset),
            LoggingSource.Preset when Preset is not null => await loggingPresets.Find(Preset),
            LoggingSource.Custom when Custom is not null => Custom,
            _ => null
        };
    }
}