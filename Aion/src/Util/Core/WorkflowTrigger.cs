using System;
using Aion.Home.Jobs;
using Aion.Util.Core.Scheduler;
using Aion.Util.Tech.Quartz;
using Quartz;

namespace Aion.Util.Core;

public static class WorkflowTrigger
{
    private static ITrigger Create
    (
        string profileName,
        string workflowName,
        Func<TriggerOptions> createOptions,
        Action<TriggerBuilder> customize
    )
    {
        var options = createOptions();
        var group = new JobGroup<WorkflowJob>(profileName);

        var builder =
            TriggerBuilder
                .Create()
                .ForJob(workflowName, group)
                .WithIdentity(workflowName, group)
                .UsingJobData(JobDataKeys.WorkflowName, workflowName)
                .UsingJobData(JobDataKeys.ProfileName, profileName)
                .UsingJobData(options.WorkflowMode);
        customize(builder);
        return builder.Build();
    }

    public static ITrigger Create
    (
        string profileName,
        string workflowName,
        string cronExpression
    ) => Create
    (
        profileName,
        workflowName,
        () => new TriggerOptions(WorkflowMode.Cron),
        builder => builder.WithCronSchedule(cronExpression));

    public static ITrigger Create
    (
        string profileName,
        string workflowName,
        DateTimeOffset startAtUtc
    ) => Create
    (
        profileName,
        workflowName,
        () => new TriggerOptions(WorkflowMode.User),
        builder => builder.StartAt(startAtUtc).WithSimpleSchedule(x => x.WithRepeatCount(0))
    );
}

public record TriggerOptions(WorkflowMode WorkflowMode);