using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Home.Jobs;
using Aion.Meta.Logging;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Services.Scheduling;

// https://www.quartz-scheduler.net/documentation/quartz-3.x/quick-start.html

public class SchedulesWorkflowCron
(
    ILogger<SchedulesWorkflowCron> logger,
    ISchedulerFactory schedulerFactory
)
{
    public async Task<WorkflowSyncResult> For(WorkflowMatch match)
    {
        var scheduler = await schedulerFactory.GetScheduler();
        using var scope = logger.BeginScopeFrom(new { WorkflowName = match.Name });

        var jobKey = match.CronJobKey;
        var cronTrigger = match.CronTrigger;

        var syncAction = await WhatToDoAbout(match.Value, jobKey, cronTrigger);
        var deleted = syncAction switch
        {
            WorkflowSyncAction.UnscheduleBecauseDisabled => await scheduler.DeleteJob(jobKey),
            WorkflowSyncAction.UnscheduleBecauseEmpty => await scheduler.DeleteJob(jobKey),
            _ => default(bool?)
        };

        var jobDetail =
            JobBuilder
                .Create<ExecutesWorkflowCron>()
                .WithIdentity(jobKey.Name, jobKey.Group)
                .DisallowConcurrentExecution()
                .Build();

        var next = syncAction switch
        {
            WorkflowSyncAction.UpdateBecauseChanged => await scheduler.RescheduleJob(cronTrigger.Key, cronTrigger),
            WorkflowSyncAction.ScheduleBecauseNew => await scheduler.ScheduleJob(jobDetail, cronTrigger),
            _ => null
        };

        logger.LogInformation("Workflow review decision: {WorkflowReviewDecision}.", syncAction);
        if (next is not null)
        {
            logger.LogInformation("Next execution at '{Next}'.", next);
        }

        return new WorkflowSyncResult(syncAction, deleted, next);
    }

    public async Task<WorkflowSyncAction> WhatToDoAbout(Workflow workflow, JobKey workflowKey, ICronTrigger trigger)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        if (!workflow.IsOn)
        {
            if (await scheduler.CheckExists(workflowKey))
            {
                return WorkflowSyncAction.UnscheduleBecauseDisabled;
            }

            return WorkflowSyncAction.IgnoreBecauseDisabled;
        }

        if (!workflow.Steps.Any(s => s.IsOn))
        {
            if (await scheduler.CheckExists(workflowKey))
            {
                return WorkflowSyncAction.UnscheduleBecauseEmpty;
            }

            return WorkflowSyncAction.IgnoreBecauseEmpty;
        }

        if (await scheduler.GetTrigger(trigger.Key) is ICronTrigger { CronExpressionString: { } cron } current)
        {
            if (cron.Equals(trigger.CronExpressionString))
            {
                return WorkflowSyncAction.IgnoreBecauseUnchanged;
            }

            return WorkflowSyncAction.UpdateBecauseChanged;
        }

        return WorkflowSyncAction.ScheduleBecauseNew;
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
    ScheduleBecauseNew
}

public record WorkflowSyncResult
(
    WorkflowSyncAction Action,
    bool? Deleted = null,
    DateTimeOffset? NextUtc = null
);

public class WorkflowAlreadyScheduledException : Exception;