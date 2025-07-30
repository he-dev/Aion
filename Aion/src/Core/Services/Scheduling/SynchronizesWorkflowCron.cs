using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Home.Jobs;
using Aion.Util.Quartz;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Services.Scheduling;

// https://www.quartz-scheduler.net/documentation/quartz-3.x/quick-start.html

public class SynchronizesWorkflowCron
(
    ILogger<SynchronizesWorkflowCron> logger,
    ISchedulerFactory schedulerFactory
)
{
    public async Task<WorkflowSynchronizationSummary> For(string profileName, Workflow workflow)
    {
        var scheduler = await schedulerFactory.GetScheduler();
        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflow.Name });

        var syncAction = await WhatToDoAbout(profileName, workflow);
        var deleted = syncAction switch
        {
            WorkflowSynchronizationAction.UnscheduleBecauseDisabled => await scheduler.DeleteJob(workflow.CreatesJobKey(profileName)),
            WorkflowSynchronizationAction.UnscheduleBecauseEmpty => await scheduler.DeleteJob(workflow.CreatesJobKey(profileName)),
            _ => default(bool?)
        };

        var trigger = workflow.CreatesCronTrigger(profileName);
        var jobDetail =
            JobBuilder
                .Create<ExecutesWorkflowCron>()
                .WithIdentity(workflow.Name, JobGroupName.From<ExecutesWorkflowCron>(profileName))
                .UsingJobData(JobDataKeys.ProfileName, profileName)
                .UsingJobData(JobDataKeys.WorkflowName, workflow.Path)
                .Build();

        var next = syncAction switch
        {
            WorkflowSynchronizationAction.UpdateBecauseChanged => await scheduler.RescheduleJob(trigger.Key, trigger),
            WorkflowSynchronizationAction.ScheduleBecauseNew => await scheduler.ScheduleJob(jobDetail, workflow.CreatesCronTrigger(profileName)),
            _ => null
        };

        logger.LogInformation("Workflow review decision: {WorkflowReviewDecision}.", syncAction);
        if (next is not null)
        {
            logger.LogInformation("Next execution at '{Next}'.", next);
        }

        return new WorkflowSynchronizationSummary(syncAction, deleted, next);
    }

    public async Task<WorkflowSynchronizationAction> WhatToDoAbout(string profileName, Workflow workflow)
    {
        var scheduler = await schedulerFactory.GetScheduler();
        var workflowKey = workflow.CreatesJobKey(profileName);

        if (!workflow.IsOn)
        {
            if (await scheduler.CheckExists(workflowKey))
            {
                return WorkflowSynchronizationAction.UnscheduleBecauseDisabled;
            }

            return WorkflowSynchronizationAction.IgnoreBecauseDisabled;
        }

        if (!workflow.Steps.Any(s => s.IsOn))
        {
            if (await scheduler.CheckExists(workflowKey))
            {
                return WorkflowSynchronizationAction.UnscheduleBecauseEmpty;
            }

            return WorkflowSynchronizationAction.IgnoreBecauseEmpty;
        }

        // .. This might throw when the Cron property is invalid.
        var trigger = workflow.CreatesCronTrigger(profileName);

        if (await scheduler.GetTrigger(trigger.Key) is ICronTrigger { CronExpressionString: { } cron } current)
        {
            if (cron.Equals(trigger.CronExpressionString))
            {
                return WorkflowSynchronizationAction.IgnoreBecauseUnchanged;
            }

            return WorkflowSynchronizationAction.UpdateBecauseChanged;
        }

        return WorkflowSynchronizationAction.ScheduleBecauseNew;
    }
}

public enum WorkflowSynchronizationAction
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
    WorkflowSynchronizationAction Action,
    bool? Deleted = null,
    DateTimeOffset? NextUtc = null
);



public class WorkflowAlreadyScheduledException : Exception;