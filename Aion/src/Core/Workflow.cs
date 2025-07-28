using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Core.Flairs;

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

    // .. Variables are optional and can be empty.
    public Dictionary<string, object?> Args { get; init; } = new();

    // .. Workflows without steps don't make sense, so make it a required field.
    public List<Step> Steps { get; init; } = [];

    [JsonPropertyName("SerilogOrPresetInfo")]
    public JsonObject? Logging { get; init; }

    #region Meta

    // .. A couple of extra fields that are being set after the workflow has been loaded.

    public string Path { get; init; } = string.Empty;

    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

    #endregion

    // !! We need to ensure workflows are unique since we can load them both from JSON, or YAML.
    public virtual bool Equals(Workflow? other)
    {
        return other is not null && StringComparer.OrdinalIgnoreCase.Equals(Name, other.Name);
    }

    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
    }

    public record Step
    {
        public string? Name { get; init; }

        [JsonIgnore]
        public int Index { get; init; }

        public bool IsOn { get; init; } = true;

        public string File { get; init; } = null!;

        public List<string> Args { get; init; } = [];

        public string? WorkingDirectory { get; init; }

        public TimeSpan Timeout { get; init; } = System.Threading.Timeout.InfiniteTimeSpan;

        [JsonPropertyName("SerilogOrPresetInfo")]
        public JsonObject? Logging { get; init; }

        public string? DependsOn { get; init; }

        // !! We need to ensure steps are unique.
        public virtual bool Equals(Step? other)
        {
            return other is not null && StringComparer.OrdinalIgnoreCase.Equals(Name, other.Name);
        }

        public override int GetHashCode()
        {
            return Name is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
        }
    }

    public static async Task<Workflow> FromFile(string path)
    {
        if (!File.Exists(path))
        {
            // note: This is pretty unlikely, but who knows...
            throw new FileNotFoundException($"Workflow '{path}' not found.", fileName: path);
        }

        var workflow = await FromJson(path) ?? throw new WorkflowNullException(path);

        // meta: Update runtime properties.
        workflow = workflow with
        {
            Path = path,
            Steps = workflow.Steps.Select((step, index) => step with { Index = index }).ToList()
        };

        workflow.EnsureUrlSafeName();
        workflow.EnsureRenderable();
        workflow.EnsureSchedulable();

        return workflow;
    }

    public static async Task<Workflow?> FromJson(string path)
    {
        await using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<Workflow>(fileStream, new JsonSerializerOptions
        {
            ReadCommentHandling = JsonCommentHandling.Skip
        });
    }
}

public record WorkflowPath(string ProfilePath, string RelativePath)
{
    public override string ToString() => Path.Join(ProfilePath, RelativePath);

    public static implicit operator string(WorkflowPath path) => path.ToString();
}

public class WorkflowNullException(string path) : Exception($"Workflow '{path}' is null.");