using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Modules;
using Aion.Modules.Scheduler;
using Aion.Modules.Services;
using Aion.Premise.Jobs;
using Aion.Toolbox.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Premise.Services.Commands;

public class ScheduleWorkflow
(
    ILogger<ScheduleWorkflow> logger,
    IOptionsSnapshot<SchedulerOptions> schedulerOptions,
    CreateWorkflow createWorkflow,
    ISchedulerFactory schedulerFactory
)
{
    // https://www.quartz-scheduler.net/documentation/quartz-3.x/quick-start.html

    public async Task<WorkflowSyncResult.Passed> Invoke
    (
        string profileName,
        string workflowName,
        DateTimeOffset? startAtUtc,
        IImmutableList<StepIdentifier>? stepOrder = null
    )
    {
        var profile = schedulerOptions.Value.Profiles[profileName];
        var workflowPath = profile.Workflows.Single(workflowName);
        return await Invoke(workflowPath, startAtUtc, stepOrder);
    }

    // note: This can only succeed, otherwise it throws an exception.
    public async Task<WorkflowSyncResult.Passed> Invoke
    (
        WorkflowPath workflowPath,
        DateTimeOffset? startAtUtc = null,
        IImmutableList<StepIdentifier>? stepOrder = null,
        Func<string, Task<WorkflowTemplate>>? loadTemplate = null
    )
    {
        var trigger =
            startAtUtc is not null
                ? CreateTrigger.Simple(workflowPath.ProfileName, workflowPath.WorkflowName, startAtUtc.Value)
                : null;

        var workflow = await createWorkflow.For(workflowPath, trigger, stepOrder, loadTemplate);

        var scheduler = await schedulerFactory.GetScheduler();
        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflow.Name });

        if (workflow.Trigger is not ICronTrigger)
        {
            return await AddCustom(workflow);
        }

        var syncAction = await workflow.DetermineSyncAction(scheduler);
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
}

public static class WorkflowExtensions
{
    public static async Task<WorkflowSyncAction> DetermineSyncAction(this Workflow workflow, IScheduler scheduler)
    {
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