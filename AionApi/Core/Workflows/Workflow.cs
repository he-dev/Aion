using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Quartz;
using YamlDotNet.Serialization.NamingConventions;

namespace AionApi.Workflows;

[PublicAPI]
public record Workflow
{
    // .. Make the user specify this value explicitly.
    public required bool Enabled { get; init; }

    // .. Having a schedule is the whole point of a workflow, so make it "required".
    public required string Cron { get; init; }

    // .. Variables are optional and can be empty.
    public Dictionary<string, string> Variables { get; init; } = new();

    // .. Workflows without steps don't make sense, so make it a required field.
    public required List<Step> Steps { get; init; } = [];

    // .. The name will be set after loading the config.
    [JsonIgnore]
    public string Path { get; init; } = Guid.NewGuid().ToString();

    // .. The name will be set after loading the config.
    [JsonIgnore]
    public string Name { get; init; } = Guid.NewGuid().ToString();

    [JsonIgnore]
    public JobKey JobKey => new(Name, JobGroupNames.Workflows);

    public static implicit operator bool(Workflow workflow) => workflow.Enabled;

    // !! Catch this property as it might throw when the Cron property is invalid.
    [JsonIgnore]
    public ICronTrigger Trigger =>
        (ICronTrigger)TriggerBuilder
            .Create()
            .WithIdentity(Name, JobGroupNames.Workflows)
            .WithCronSchedule(Cron)
            .Build();

    // !! We need to ensure workflows are unique since we can load them both from JSON, or YAML.
    public virtual bool Equals(Workflow? other)
    {
        return other is not null && StringComparer.OrdinalIgnoreCase.Equals(Name, other.Name);
    }

    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
    }

    public static async Task<Workflow?> FromFile(string path, string workflowDirectoryPath)
    {
        // !! Must not throw exceptions so it can be used in async loops.

        if (!File.Exists(path))
        {
            return null;
        }

        return System.IO.Path.GetExtension(path).ToLower() switch
        {
            ".json" => await FromJson(path, workflowDirectoryPath),
            ".yaml" => await FromYaml(path, workflowDirectoryPath),
            _ => throw new InvalidOperationException($"Unknown file extension: {path}")
        };
    }

    public static async Task<Workflow?> FromJson(string path, string workflowDirectoryPath)
    {
        await using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (await JsonSerializer.DeserializeAsync<Workflow>(fileStream) is { } workflow)
        {
            return workflow with { Path = path, Name = NameFactory.Create(path, workflowDirectoryPath) };
        }

        return null;
    }

    public static async Task<Workflow?> FromYaml(string path, string workflowDirectoryPath)
    {
        var yamlDeserializer = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        await using var yamlFileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var yamlStreamReader = new StreamReader(yamlFileStream);
        if (yamlDeserializer.Deserialize<Workflow>(yamlStreamReader) is { } workflow)
        {
            return workflow with
            {
                Path = path,
                Name = NameFactory.Create(path, workflowDirectoryPath),
                Steps = workflow.Steps.Select((step, index) => step with { Index = index }).ToList()
            };
        }

        return null;
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

        public string? DependsOn { get; init; }

        // !! We need to ensure steps are unique.
        public virtual bool Equals(Workflow? other)
        {
            return other is not null && StringComparer.OrdinalIgnoreCase.Equals(Name, other.Name);
        }

        public override int GetHashCode()
        {
            return Name is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
        }

        public static implicit operator bool(Step step) => step.Enabled;
    }

    // !! This class encapsulates the logic for creating workflow names.
    public static class NameFactory
    {
        public static string Create(string path, string workflowDirectoryPath)
        {
            // ?? Turns "C:\\foo\\bar\\baz.yaml" to "bar.baz" when the workflow-directory-name is "C:\\foo"
            var name = path[(workflowDirectoryPath.Length + 1)..].Replace('\\', '.');
            return name[..^System.IO.Path.GetExtension(path).Length];
        }
    }
}

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