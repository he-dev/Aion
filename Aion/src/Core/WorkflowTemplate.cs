using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Aion.Core;

public record WorkflowTemplate
{
    // core: Make the user specify this value explicitly, so they don't activate workflows by accident.
    public bool Enabled { get; init; }

    public string Cron { get; init; } = null!;

    public Dictionary<string, string>? Variables { get; init; }

    public Dictionary<string, string>? Environment { get; init; }

    public JsonObject? Logging { get; init; }

    public StepTemplate[] Steps { get; init; } = null!;

    public record StepTemplate
    {
        public string? Name { get; init; }

        public bool Enabled { get; init; } = true;

        public string FileName { get; init; } = null!;

        public string? Arguments { get; init; }

        public Dictionary<string, string>? Environment { get; init; }

        public string? WorkingDirectory { get; init; }

        public TimeSpan? Timeout { get; init; }

        public JsonObject? Logging { get; init; }

        public string? DependsOn { get; init; }
    }

    public static async Task<WorkflowTemplate> FromFile(string path)
    {
        if (!File.Exists(path))
        {
            // note: This is pretty unlikely, but who knows...
            throw new FileNotFoundException($"Workflow '{path}' not found.", fileName: path);
        }

        await using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var workflow = await JsonSerializer.DeserializeAsync<WorkflowTemplate>(fileStream, new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip
        });

        return workflow ?? throw new InvalidWorkflow(path);
    }
}

public class InvalidWorkflow(string path) : Exception($"File '{path}' is not a valid workflow.");