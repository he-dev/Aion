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

namespace Aion.Core.Workflows;

[PublicAPI]
public record Workflow
{
    // .. Make the user specify this value explicitly.
    public required bool Enabled { get; init; }

    // .. Having a schedule is the whole point of a workflow, so make it "required".
    public required string Cron { get; init; }

    // .. Variables are optional and can be empty.
    public Dictionary<string, object?> Variables { get; init; } = new();

    // .. Workflows without steps don't make sense, so make it a required field.
    public List<Step> Steps { get; init; } = [];

    // .. The name will be set after loading the config.
    //[JsonIgnore]
    //public string Path { get; init; } = Guid.NewGuid().ToString();

    // .. The name will be set after loading the config.
    //[JsonIgnore]
    //public string Name { get; init; } = Guid.NewGuid().ToString();

    [JsonIgnore]
    public Meta Info { get; init; } = null!;

    [JsonIgnore]
    public JobKey JobKey => new(Info.Name, JobGroupNames.Workflows);

    public static implicit operator bool(Workflow workflow)
    {
        return workflow.Enabled && workflow.Steps.Any(s => s.Enabled) && workflow.Info.Exception is not null;
    }

    // !! Catch this property as it might throw when the Cron property is invalid.
    [JsonIgnore]
    public ICronTrigger Trigger =>
        (ICronTrigger)TriggerBuilder
            .Create()
            .WithIdentity(Info.Name, JobGroupNames.Workflows)
            .WithCronSchedule(Cron)
            .Build();

    // !! We need to ensure workflows are unique since we can load them both from JSON, or YAML.
    public virtual bool Equals(Workflow? other)
    {
        return other is not null && StringComparer.OrdinalIgnoreCase.Equals(Info.Name, other.Info.Name);
    }

    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Info.Name);
    }

    public static async Task<Workflow> FromFile(string path, string root)
    {
        // !! Do not handle exception here because there is no logger. Let the caller deal with them.

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Workflow '{path}' not found.", fileName: path);
        }

        var workflow = Path.GetExtension(path).ToLower() switch
        {
            ".json" => await FromJson(path),
            ".yaml" => await FromYaml(path),
            _ => throw new InvalidOperationException($"Unknown file extension: {path}")
        } ?? throw new InvalidOperationException($"Error loading workflow '{path}'.");

        workflow = workflow with
        {
            Info = new Meta
            {
                Root = root,
                Path = path
            },
            Steps = workflow.Steps.Select((step, index) => step with { Index = index }).ToList()
        };

        // .. Now, initialize properties that require the Info property to exist.
        return workflow with
        {
            Info = workflow.Info with
            {
                // ?? Create the trigger eagerly so that cron expression exceptions can be thrown early.
                Trigger =
                TriggerBuilder
                    .Create()
                    .WithIdentity(workflow.Info.Name, JobGroupNames.Workflows)
                    .WithCronSchedule(workflow.Cron)
                    .Build() as ICronTrigger
            }
        };
    }

    public static async Task<Workflow?> FromJson(string path)
    {
        await using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<Workflow>(fileStream);
    }

    public static async Task<Workflow?> FromYaml(string path)
    {
        var yamlDeserializer = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        await using var yamlFileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var yamlStreamReader = new StreamReader(yamlFileStream);
        return yamlDeserializer.Deserialize<Workflow>(yamlStreamReader);
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
        public virtual bool Equals(Workflow? other)
        {
            return other is not null && StringComparer.OrdinalIgnoreCase.Equals(Name, other.Info.Name);
        }

        public override int GetHashCode()
        {
            return Name is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
        }

        public static implicit operator bool(Step step) => step.Enabled;
    }

    public record Meta
    {
        public required string Root { get; init; }
        public required string Path { get; init; }

        public string Name
        {
            get
            {
                // !! Use the names that remain after dropping the root as the name.
                var name = Path[(Root.Length + 1)..].Replace('\\', '.');
                return name[..^System.IO.Path.GetExtension(Path).Length];
            }
        }

        public Exception? Exception { get; init; }

        public JobKey JobKey => new(Name, JobGroupNames.Workflows);

        public ICronTrigger? Trigger { get; init; }
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