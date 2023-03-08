using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Aion.Jobs;
using Newtonsoft.Json;
using Quartz;
using Quartz.Impl.Triggers;
using Reusable.Collections;
using Reusable.Extensions;

namespace Aion.Data;

[JsonObject]
public class Workflow : IEquatable<Workflow>
{
    [DefaultValue(true)]
    public bool Enabled { get; set; }

    [JsonRequired]
    public string Schedule { get; set; } = null!;

    [JsonIgnore]
    [AutoEqualityProperty]
    public string Name { get; set; } = null!;

    [DefaultValue(false)]
    public bool StartImmediately { get; set; }

    [JsonRequired]
    public List<Step> Steps { get; set; } = new();

    //[JsonIgnore]
    //public JobKey JobKey => new(Name);

    //[JsonIgnore]
    //public TriggerKey TriggerKey => new(Name);

    public override string ToString() => Path.GetFileNameWithoutExtension(Name);

    public static implicit operator string(Workflow workflow) => workflow.ToString();

    #region IEquatable<>

    public bool Equals(Workflow? other) => EqualityComparer<Workflow>.Default.Equals(this, other);

    public override bool Equals(object? obj) => Equals(obj as Workflow);

    public override int GetHashCode() => EqualityComparer<Workflow>.Default.GetHashCode(this);

    #endregion
}

public static class WorkflowExtensions
{
    public static IJobDetail ToJobDetail(this Workflow workflow, Action<JobBuilder> configure)
    {
        return
            JobBuilder
                .Create<WorkflowLauncher>()
                .WithIdentity(workflow.Name, nameof(Workflow))
                .Also(configure)
                .Build();
    }

    public static ITrigger ToTrigger(this Workflow workflow, Action<TriggerBuilder>? configure = null)
    {
        return
            TriggerBuilder
                .Create()
                .WithIdentity(workflow.Name, nameof(Workflow))
                .WithCronSchedule(workflow.Schedule)
                .StartImmediately(workflow)
                .Also(configure)
                .Build();
    }

    private static TriggerBuilder StartImmediately(this TriggerBuilder builder, Workflow workflow)
    {
        return
            workflow.StartImmediately
                ? builder.StartAt(DateTimeOffset.UtcNow.AddDays(-1))
                : builder;
    }

    public static IEnumerable<Workflow> ToWorkflows(this IEnumerable<ITrigger> triggers)
    {
        return triggers.OfType<CronTriggerImpl>().Select(t => new Workflow
        {
            Enabled = true,
            Name = t.Key.Name,
            Schedule = t.CronExpressionString!
        });
    }
}