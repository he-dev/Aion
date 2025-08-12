using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Home.Jobs;
using Aion.Util.Logging;
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
    public async Task<WorkflowSyncResult> For(Workflow workflow)
    {
        var scheduler = await schedulerFactory.GetScheduler();
        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflow.Name });

        var syncAction = await WhatToDoAbout(workflow);
        var deleted = syncAction switch
        {
            WorkflowSyncAction.UnscheduleBecauseDisabled => await scheduler.DeleteJob(workflow.CronJobKey),
            WorkflowSyncAction.UnscheduleBecauseEmpty => await scheduler.DeleteJob(workflow.CronJobKey),
            _ => default(bool?)
        };

        var jobDetail =
            JobBuilder
                .Create<ExecutesWorkflowCron>()
                .WithIdentity(workflow.CronJobKey.Name, workflow.CronJobKey.Group)
                .DisallowConcurrentExecution()
                .Build();

        var next = syncAction switch
        {
            WorkflowSyncAction.UpdateBecauseChanged => await scheduler.RescheduleJob(workflow.CronTrigger.Key, workflow.CronTrigger),
            WorkflowSyncAction.ScheduleBecauseNew => await scheduler.ScheduleJob(jobDetail, workflow.CronTrigger),
            _ => null
        };

        logger.LogInformation("Workflow review decision: {WorkflowReviewDecision}.", syncAction);
        if (next is not null)
        {
            logger.LogInformation("Next execution at '{Next}'.", next);
        }

        return new WorkflowSyncResult(syncAction, deleted, next);
    }

    public async Task<WorkflowSyncAction> WhatToDoAbout(Workflow workflow)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        if (!workflow.Enabled)
        {
            if (await scheduler.CheckExists(workflow.CronJobKey))
            {
                return WorkflowSyncAction.UnscheduleBecauseDisabled;
            }

            return WorkflowSyncAction.IgnoreBecauseDisabled;
        }

        if (!workflow.Steps.Any(s => s.Enabled))
        {
            if (await scheduler.CheckExists(workflow.CronJobKey))
            {
                return WorkflowSyncAction.UnscheduleBecauseEmpty;
            }

            return WorkflowSyncAction.IgnoreBecauseEmpty;
        }

        if (await scheduler.GetTrigger(workflow.CronTrigger.Key) is ICronTrigger { CronExpressionString: { } cron } current)
        {
            if (cron.Equals(workflow.CronTrigger.CronExpressionString))
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