using System;
using System.Collections.Generic;
using System.IO;
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
    public required List<Step> Steps { get; init; }

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

    public static async Task<Workflow?> FromFile(string fileName)
    {
        // !! Must not throw exceptions so it can be used in async loops.

        if (!File.Exists(fileName))
        {
            return null;
        }

        // await using var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        // if (await JsonSerializer.DeserializeAsync<Workflow>(fileStream) is { } workflow)
        // {
        //     return workflow with { Name = fileName };
        // }

        //var yamlContent = await File.ReadAllTextAsync(fileName);
        var yamlDeserializer = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        await using var yamlFileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var yamlStreamReader = new StreamReader(yamlFileStream);
        if (yamlDeserializer.Deserialize<Workflow>(yamlStreamReader) is { } workflow)
        {
            return workflow;
        }

        return null;
    }

    [PublicAPI]
    public record Step
    {
        public string? Name { get; init; }

        public string Script { get; init; } = null!;

        public List<string> Args { get; init; } = new();

        public string? WorkingDirectory { get; init; }

        public int TimeoutMilliseconds { get; init; } = -1;

        public bool Enabled { get; init; } = true;

        public string? DependsOn { get; init; }

        public static implicit operator bool(Step step) => step.Enabled;
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