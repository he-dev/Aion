using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Aion.Home.Jobs;
using Aion.Util.Logging;
using Aion.Util.Quartz;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

namespace Aion.Core.Workflows;

// https://www.quartz-scheduler.net/documentation/quartz-3.x/quick-start.html

public class WorkflowScheduleRegistry
(
    ILogger<WorkflowScheduleRegistry> logger,
    ISchedulerFactory schedulerFactory
)
{
    // note: This can only succeed, otherwise it throws an exception.
    public async Task<WorkflowSyncResult.Passed> AddOrUpdate(Workflow workflow)
    {
        var scheduler = await schedulerFactory.GetScheduler();
        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflow.Name });

        if (workflow.Trigger is not ICronTrigger)
        {
            return await AddCustom(workflow);
        }

        var syncAction = await WhatToDoAbout(workflow);
        var deleted = syncAction switch
        {
            WorkflowSyncAction.UnscheduleBecauseDisabled => await scheduler.DeleteJob(workflow.Trigger.JobKey),
            WorkflowSyncAction.UnscheduleBecauseEmpty => await scheduler.DeleteJob(workflow.Trigger.JobKey),
            _ => default(bool?)
        };

        var jobDetail =
            JobBuilder
                .Create<WorkflowJob>()
                .WithIdentity(workflow.Trigger.JobKey)
                .DisallowConcurrentExecution()
                .Build();

        var next = syncAction switch
        {
            WorkflowSyncAction.UpdateBecauseChanged => await scheduler.RescheduleJob(workflow.Trigger.Key, workflow.Trigger),
            WorkflowSyncAction.ScheduleBecauseNew => await scheduler.ScheduleJob(jobDetail, workflow.Trigger),
            _ => null
        };

        logger.LogInformation("Workflow review decision: {WorkflowReviewDecision}.", syncAction);
        if (next is not null)
        {
            logger.LogInformation("Next execution at '{Next}'.", next);
        }

        return new WorkflowSyncResult.Passed
        {
            Path = workflow.Path,
            Action = syncAction,
            Deleted = deleted,
            NextUtc = next
        };
    }

    public async Task<WorkflowSyncAction> WhatToDoAbout(Workflow workflow)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        if (!workflow.Enabled)
        {
            if (await scheduler.CheckExists(workflow.Trigger.JobKey))
            {
                return WorkflowSyncAction.UnscheduleBecauseDisabled;
            }

            return WorkflowSyncAction.IgnoreBecauseDisabled;
        }

        if (!workflow.Steps.Any(s => s.Enabled))
        {
            if (await scheduler.CheckExists(workflow.Trigger.JobKey))
            {
                return WorkflowSyncAction.UnscheduleBecauseEmpty;
            }

            return WorkflowSyncAction.IgnoreBecauseEmpty;
        }

        if (await scheduler.GetTrigger(workflow.Trigger.Key) is ICronTrigger { CronExpressionString: { } currentCron })
        {
            if (workflow.Trigger is ICronTrigger { CronExpressionString: { } otherCron } && currentCron.Equals(otherCron))
            {
                return WorkflowSyncAction.IgnoreBecauseUnchanged;
            }

            return WorkflowSyncAction.UpdateBecauseChanged;
        }

        return WorkflowSyncAction.ScheduleBecauseNew;
    }

    private async Task<WorkflowSyncResult.Passed> AddCustom(Workflow workflow)
    {
        var jobDetail =
            JobBuilder
                .Create<WorkflowJob>()
                .WithIdentity(workflow.Trigger.JobKey)
                .Build();

        var scheduler = await schedulerFactory.GetScheduler();

        // core: Ensure the job is not already scheduled.
        if (await scheduler.CheckExists(jobDetail.Key))
        {
            throw new WorkflowAlreadyScheduledException();
        }

        logger.LogInformation("Workflow '{WorkflowName}' will be executed once at '{Next}'.", workflow.Name, workflow.Trigger.GetNextFireTimeUtc());

        var next = await scheduler.ScheduleJob(jobDetail, workflow.Trigger);
        return new WorkflowSyncResult.Passed
        {
            Path = workflow.Path,
            Action = WorkflowSyncAction.ScheduleBecauseCustom,
            Deleted = false,
            NextUtc = next
        };
    }

    public async Task<bool> Remove(JobKey jobKey)
    {
        var scheduler = await schedulerFactory.GetScheduler();
        return await scheduler.DeleteJob(jobKey);
    }

    public async IAsyncEnumerable<ITrigger> EnumerateTriggersFor(string profileName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var groupMatcher = GroupMatcher<JobKey>.GroupEquals(new JobGroup<WorkflowJob>(profileName));

        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobKeys = await scheduler.GetJobKeys(groupMatcher, cancellationToken);
        foreach (var jobKey in jobKeys)
        {
            foreach (var trigger in await scheduler.GetTriggersOfJob(jobKey, cancellationToken))
            {
                yield return trigger;
            }
        }
    }
}

public enum WorkflowSyncAction
{
    IgnoreBecauseDisabled,
    IgnoreBecauseEmpty,
    IgnoreBecauseUnchanged,
    UnscheduleBecauseDisabled,
    UnscheduleBecauseEmpty,
    UpdateBecauseChanged,
    ScheduleBecauseNew,
    ScheduleBecauseCustom
}

public record WorkflowSyncResult
{
    public required string Path { get; init; }

    public record Passed : WorkflowSyncResult
    {
        public required WorkflowSyncAction Action { get; init; }
        public required bool? Deleted { get; init; }
        public required DateTimeOffset? NextUtc { get; init; }
        public DateTimeOffset? NextLocal => NextUtc?.ToLocalTime();
    }

    public record Failed : WorkflowSyncResult
    {
        public required Exception Exception { get; init; }
    }
}

public class WorkflowAlreadyScheduledException : Exception;