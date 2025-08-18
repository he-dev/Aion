using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Services.Jobs;
using Aion.Util.Entities.Quartz;
using Aion.Util.Logging;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

namespace Aion.Core.Entities;

// https://www.quartz-scheduler.net/documentation/quartz-3.x/quick-start.html

public class WorkflowScheduleRegistry
(
    ILogger<WorkflowScheduleRegistry> logger,
    ISchedulerFactory schedulerFactory
)
{
    public async Task<WorkflowSyncResult> AddOrUpdate(Workflow workflow)
    {
        var scheduler = await schedulerFactory.GetScheduler();
        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflow.Name });

        var trigger = workflow.CreateTrigger(null);

        var syncAction = await WhatToDoAbout(workflow, trigger);
        var deleted = syncAction switch
        {
            WorkflowSyncAction.UnscheduleBecauseDisabled => await scheduler.DeleteJob(trigger.JobKey),
            WorkflowSyncAction.UnscheduleBecauseEmpty => await scheduler.DeleteJob(trigger.JobKey),
            _ => default(bool?)
        };

        var jobDetail =
            JobBuilder
                .Create<WorkflowExecutionJob>()
                .WithIdentity(trigger.JobKey)
                .DisallowConcurrentExecution()
                .Build();

        var next = syncAction switch
        {
            WorkflowSyncAction.UpdateBecauseChanged => await scheduler.RescheduleJob(trigger.Key, trigger),
            WorkflowSyncAction.ScheduleBecauseNew => await scheduler.ScheduleJob(jobDetail, trigger),
            _ => null
        };

        logger.LogInformation("Workflow review decision: {WorkflowReviewDecision}.", syncAction);
        if (next is not null)
        {
            logger.LogInformation("Next execution at '{Next}'.", next);
        }

        return new WorkflowSyncResult(syncAction, deleted, next);
    }

    public async Task<WorkflowSyncAction> WhatToDoAbout(Workflow workflow, ITrigger trigger)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        if (!workflow.Enabled)
        {
            if (await scheduler.CheckExists(trigger.JobKey))
            {
                return WorkflowSyncAction.UnscheduleBecauseDisabled;
            }

            return WorkflowSyncAction.IgnoreBecauseDisabled;
        }

        if (!workflow.Steps.Any(s => s.Enabled))
        {
            if (await scheduler.CheckExists(trigger.JobKey))
            {
                return WorkflowSyncAction.UnscheduleBecauseEmpty;
            }

            return WorkflowSyncAction.IgnoreBecauseEmpty;
        }

        if (await scheduler.GetTrigger(trigger.Key) is ICronTrigger { CronExpressionString: { } currentCron })
        {
            if (trigger is ICronTrigger { CronExpressionString: { } otherCron } && currentCron.Equals(otherCron))
            {
                return WorkflowSyncAction.IgnoreBecauseUnchanged;
            }

            return WorkflowSyncAction.UpdateBecauseChanged;
        }

        return WorkflowSyncAction.ScheduleBecauseNew;
    }

    public async Task<WorkflowSyncResult> AddCustom(Workflow workflow, DateTimeOffset? startsOneAtUtc)
    {
        var trigger = workflow.CreateTrigger(startsOneAtUtc);

        var jobDetail =
            JobBuilder
                .Create<WorkflowExecutionJob>()
                .WithIdentity(trigger.JobKey)
                .Build();

        var scheduler = await schedulerFactory.GetScheduler();

        // core: Ensure the job is not already scheduled.
        if (await scheduler.CheckExists(jobDetail.Key))
        {
            throw new WorkflowAlreadyScheduledException();
        }

        logger.LogInformation("Workflow '{WorkflowName}' will be executed once at '{Next}'.", workflow.Name, trigger.GetNextFireTimeUtc());

        var next = await scheduler.ScheduleJob(jobDetail, trigger);
        return new WorkflowSyncResult(WorkflowSyncAction.ScheduleBecauseCustom, false, next);
    }

    public async Task<bool> Remove(JobKey jobKey)
    {
        var scheduler = await schedulerFactory.GetScheduler();
        return await scheduler.DeleteJob(jobKey);
    }

    public async IAsyncEnumerable<ITrigger> EnumerateTriggersFor(string profileName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var groupMatcher = GroupMatcher<JobKey>.GroupEquals(GroupName.For<WorkflowExecutionJob>(profileName, WorkflowExecutionMode.Cron));

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
(
    WorkflowSyncAction Action,
    bool? Deleted = null,
    DateTimeOffset? NextUtc = null
);

public class WorkflowAlreadyScheduledException : Exception;