using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Quartz;

namespace AionApi.Models;

[PublicAPI]
public class Workflow
{
    public bool Enabled { get; set; } = true;

    public string Name { get; set; } = default!;

    public string Schedule { get; set; } = default!;

    public List<string> Imports { get; set; } = new();

    public Dictionary<string, object> Variables { get; set; } = new();

    public List<Command> Commands { get; set; } = new();

    [JsonIgnore]
    public bool IsEmpty => !Commands.Any(cmd => cmd.Enabled);

    [JsonIgnore]
    public JobBuilder JobBuilder => JobBuilder.Create<Jobs.WorkflowLauncher>().WithIdentity(Name, JobGroupNames.Workflows);

    [JsonIgnore]
    public JobKey JobKey => JobBuilder.Build().Key;

    [JsonIgnore]
    public ICronTrigger CronTrigger => (ICronTrigger)TriggerBuilder.Create().WithIdentity(Name, JobGroupNames.Workflows).WithCronSchedule(Schedule).Build();

    [PublicAPI]
    public class Command
    {
        public string? Name { get; set; }

        public string FileName { get; set; } = default!;

        public List<string> Arguments { get; set; } = new();

        public string WorkingDirectory { get; set; } = string.Empty;

        public int TimeoutMilliseconds { get; set; } = -1;

        public bool Enabled { get; set; } = true;

        public string? DependsOn { get; set; }

        public static implicit operator bool(Command command) => command.Enabled;
    }

    public static implicit operator bool(Workflow workflow) => workflow.Enabled;
}




public class WorkflowBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        //var workflow = await JsonSerializer.DeserializeAsync<Workflow>(bindingContext.HttpContext.Request.BodyReader.AsStream());
        //workflow.Name = bindingContext.HttpContext.Request.Path;
        //bindingContext.Result = ModelBindingResult.Success(workflow);
        return Task.CompletedTask;
    }
}