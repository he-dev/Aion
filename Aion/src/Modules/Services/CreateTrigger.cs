using System;
using Aion.Context.Jobs;
using Aion.Modules.Scheduler;
using Aion.Toolbox.Quartz;
using Quartz;

namespace Aion.Modules.Services;

public static class CreateTrigger
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

    public static ITrigger Cron
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

    public static ITrigger Simple
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