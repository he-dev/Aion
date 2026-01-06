using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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

    public LoggingConfiguration Logging { get; init; } = new();

    public StepTemplate[] Steps { get; init; } = null!;

    public record StepTemplate
    {
        public string? Name { get; init; }

        public bool Enabled { get; init; } = true;

        [NotNullOrWhiteSpace]
        public string FileName { get; init; } = null!;

        [JsonConverter(typeof(StepArgumentConverter))]
        public IImmutableList<StepArgument> Arguments { get; init; } = [];

        public Dictionary<string, string> Environment { get; init; } = new();

        public string? WorkingDirectory { get; init; }

        public TimeSpan? Timeout { get; init; }

        public LoggingConfiguration Logging { get; init; } = new();

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

[Flags]
public enum LoggingTarget
{
    None = 0x0,
    Self = 0x1,
    Main = 0x2
}

public record LoggingConfiguration
{
    public LoggingSource Source { get; init; } = LoggingSource.Auto;

    [JsonConverter(typeof(LoggingPresetExpressionConverter))]
    public LoggingPresetExpression? Preset { get; init; }

    public JsonObject? Custom { get; init; }

    [JsonConverter(typeof(FlagsEnumConverter<LoggingTarget>))]
    public LoggingTarget Target { get; init; } = LoggingTarget.Self;
}

public class FlagsEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
        {
            return default;
        }

        var parts = value.Split('|', ',').Select(p => p.Trim());
        var result = 0;

        foreach (var part in parts)
        {
            if (Enum.TryParse<T>(part, ignoreCase: true, out var parsed))
            {
                result |= Convert.ToInt32(parsed);
            }
            else
            {
                throw new JsonException($"Unknown enum value: '{part}'");
            }
        }

        return (T)(object)result;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}