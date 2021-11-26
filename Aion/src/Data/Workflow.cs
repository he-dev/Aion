using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Aion.Jobs;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Quartz;
using Quartz.Impl;
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
    public List<Step> Steps { get; set; } = new List<Step>();

    [JsonIgnore]
    public JobKey JobKey => new(Name);

    [JsonIgnore]
    public TriggerKey TriggerKey => new(Name);

    public IJobDetail CreateJobDetail(Action<JobBuilder> configure)
    {
        return
            JobBuilder
                .Create<WorkflowLauncher>()
                .WithIdentity(Name, nameof(Workflow))
                .Also(configure)
                .Build();
    }

    public ITrigger CreateTrigger(Action<TriggerBuilder>? configure = null)
    {
        return
            TriggerBuilder
                .Create()
                .WithIdentity(new TriggerKey(Name, nameof(Workflow)))
                .StartAt(StartImmediately ? DateTime.Now.AddDays(-1) : DateTime.Now)
                .WithCronSchedule(Schedule)
                .Also(configure)
                .Build();
    }

    public override string ToString() => Path.GetFileNameWithoutExtension(Name);

    public static implicit operator string(Workflow workflow) => workflow.ToString();

    #region IEquatable<>

    public bool Equals(Workflow? other) => EqualityComparer<Workflow>.Default.Equals(this, other);

    public override bool Equals(object? obj) => Equals(obj as Workflow);

    public override int GetHashCode() => EqualityComparer<Workflow>.Default.GetHashCode(this);

    #endregion
}