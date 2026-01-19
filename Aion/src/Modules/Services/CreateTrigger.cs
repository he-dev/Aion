using System;
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
        WorkflowMode workflowMode,
        Action<TriggerBuilder> customize
    )
    {
        var builder =
            TriggerBuilder
                .Create()
                .ForJob($"{profileName}:{workflowName}", profileName)
                .WithIdentity($"{profileName}:{workflowName}:{workflowMode}", profileName)
                .UsingJobData(JobDataKeys.ProfileName, profileName)
                .UsingJobData(JobDataKeys.WorkflowName, workflowName)
                .UsingJobData(workflowMode);
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
        WorkflowMode.Cron,
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
        WorkflowMode.User,
        builder => builder.StartAt(startAtUtc).WithSimpleSchedule(x => x.WithRepeatCount(0))
    );
}

public record TriggerOptions(WorkflowMode WorkflowMode);