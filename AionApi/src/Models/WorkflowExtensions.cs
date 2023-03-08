using System;
using Quartz;
using Reusable.Extensions;

namespace AionApi.Models;

public static class WorkflowExtensions
{
    public static IJobDetail ToJobDetail<T>(this Workflow workflow, Action<JobBuilder>? configure = default) where T : IJob
    {
        return
            JobBuilder
                .Create<T>()
                .WithIdentity(workflow.Name, nameof(Workflow))
                .Also(configure)
                .Build();
    }

    public static ITrigger ToCronTrigger(this Workflow workflow, Action<TriggerBuilder>? configure = null)
    {
        return
            TriggerBuilder
                .Create()
                .WithIdentity(workflow.Name, nameof(Workflow))
                .WithCronSchedule(workflow.Schedule)
                .Also(configure)
                .Build();
    }
}