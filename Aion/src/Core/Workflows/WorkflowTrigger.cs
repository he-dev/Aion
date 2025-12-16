using System;
using Aion.Core.Quartz;
using Aion.Home.Jobs;
using Aion.Util.Quartz;
using Quartz;

namespace Aion.Core.Workflows;

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
        var group = GroupName.For<WorkflowJob>(profileName, options.WorkflowMode);

        var builder =
            TriggerBuilder
                .Create()
                .ForJob(nameof(WorkflowJob), group)
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