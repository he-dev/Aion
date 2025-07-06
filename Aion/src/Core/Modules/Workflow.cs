using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Head;
using JetBrains.Annotations;
using Quartz;

namespace Aion.Core.Modules;

[PublicAPI]
public record Workflow : ITimeZoned
{
    // .. Make the user specify this value explicitly.
    public bool Enabled { get; init; }

    // .. Having a schedule is the whole point of a workflow, so make it "required".
    public string Cron { get; init; } = null!;

    public string? TimeZoneId { get; init; }

    public TimeZoneInfo TimeZone =>
        string.IsNullOrEmpty(TimeZoneId)
            ? TimeZoneInfo.Local
            : TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    // .. Variables are optional and can be empty.
    public Dictionary<string, object?> Variables { get; init; } = new();

    // .. Workflows without steps don't make sense, so make it a required field.
    public List<Step> Steps { get; init; } = [];

    #region Meta

    // .. A couple of extra fields that are being set after the workflow has been loaded.

    public string Path { get; init; } = string.Empty;

    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

    [JsonIgnore]
    public JobKey JobKey => new(Name, JobGroupNames.Workflows);

    // !! Catch this property as it might throw when the Cron property is invalid.
    [JsonIgnore]
    public ICronTrigger Trigger =>
        (ICronTrigger)TriggerBuilder
            .Create()
            .WithIdentity(Name, JobGroupNames.Workflows)
            .UsingJobData(nameof(Path), Path)
            .WithCronSchedule(Cron, x => x.InTimeZone(TimeZone))
            .Build();

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

    [PublicAPI]
    public record Step
    {
        public string? Name { get; init; }

        [JsonIgnore]
        public int Index { get; init; }

        public bool Enabled { get; init; } = true;

        public string Script { get; init; } = null!;

        public List<string> Args { get; init; } = [];

        public string? WorkingDirectory { get; init; }

        public int TimeoutMilliseconds { get; init; } = -1;

        public bool WindowVisible { get; init; }

        public bool LogStdOut { get; init; }

        public bool LogStdErr { get; init; }

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

        public static implicit operator bool(Step step) => step.Enabled;
    }

    public static async Task<Workflow> FromFile(string path)
    {
        // todo: check path for characters that are illegal in http urls

        if (!File.Exists(path))
        {
            // This is pretty unlikely, but who knows...
            throw new FileNotFoundException($"Workflow '{path}' not found.", fileName: path);
        }

        var workflow = await FromJson(path) ?? throw new WorkflowNullException(path);

        // .. Update meta-properties.
        workflow = workflow with
        {
            Path = path,
            Steps = workflow.Steps.Select((step, index) => step with { Index = index }).ToList()
        };

        workflow.EnsureRenderable();
        workflow.EnsureSchedulable();

        return workflow;
    }

    public static async Task<Workflow?> FromJson(string path)
    {
        await using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<Workflow>(fileStream);
    }
}

public class WorkflowNullException(string path) : Exception($"Workflow '{path}' is null.");


// public class WorkflowBinder : IModelBinder
// {
//     public Task BindModelAsync(ModelBindingContext bindingContext)
//     {
//         //var workflow = await JsonSerializer.DeserializeAsync<Workflow>(bindingContext.HttpContext.Request.BodyReader.AsStream());
//         //workflow.Name = bindingContext.HttpContext.Request.Path;
//         //bindingContext.Result = ModelBindingResult.Success(workflow);
//         return Task.CompletedTask;
//     }
// }