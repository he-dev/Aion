using System;
using System.Diagnostics.CodeAnalysis;
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
    public async Task<WorkflowSynchronizationSummary> For(WorkflowMatch match)
    {
        await match.Load();
        var scheduler = await schedulerFactory.GetScheduler();
        using var scope = logger.BeginScopeFrom(new { WorkflowName = match.Name });

        var jobKey = match.CronJobKey;
        var cronTrigger = match.CronTrigger;

        var syncAction = await WhatToDoAbout(match.Value, jobKey, cronTrigger);
        var deleted = syncAction switch
        {
            WorkflowAction.UnscheduleBecauseDisabled => await scheduler.DeleteJob(jobKey),
            WorkflowAction.UnscheduleBecauseEmpty => await scheduler.DeleteJob(jobKey),
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
            WorkflowAction.UpdateBecauseChanged => await scheduler.RescheduleJob(cronTrigger.Key, cronTrigger),
            WorkflowAction.ScheduleBecauseNew => await scheduler.ScheduleJob(jobDetail, cronTrigger),
            _ => null
        };

        logger.LogInformation("Workflow review decision: {WorkflowReviewDecision}.", syncAction);
        if (next is not null)
        {
            logger.LogInformation("Next execution at '{Next}'.", next);
        }

        return new WorkflowSynchronizationSummary(syncAction, deleted, next);
    }

    public async Task<WorkflowAction> WhatToDoAbout(Workflow workflow, JobKey workflowKey, ICronTrigger trigger)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        if (!workflow.IsOn)
        {
            if (await scheduler.CheckExists(workflowKey))
            {
                return WorkflowAction.UnscheduleBecauseDisabled;
            }

            return WorkflowAction.IgnoreBecauseDisabled;
        }

        if (!workflow.Steps.Any(s => s.IsOn))
        {
            if (await scheduler.CheckExists(workflowKey))
            {
                return WorkflowAction.UnscheduleBecauseEmpty;
            }

            return WorkflowAction.IgnoreBecauseEmpty;
        }

        if (await scheduler.GetTrigger(trigger.Key) is ICronTrigger { CronExpressionString: { } cron } current)
        {
            if (cron.Equals(trigger.CronExpressionString))
            {
                return WorkflowAction.IgnoreBecauseUnchanged;
            }

            return WorkflowAction.UpdateBecauseChanged;
        }

        return WorkflowAction.ScheduleBecauseNew;
    }
}

public enum WorkflowAction
{
    IgnoreBecauseDisabled,
    IgnoreBecauseEmpty,
    IgnoreBecauseUnchanged,
    UnscheduleBecauseDisabled,
    UnscheduleBecauseEmpty,
    UpdateBecauseChanged,
    ScheduleBecauseNew
}

public record WorkflowSynchronizationSummary
(
    WorkflowAction Action,
    bool? Deleted = null,
    DateTimeOffset? NextUtc = null
);

public class WorkflowAlreadyScheduledException : Exception;