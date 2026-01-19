using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Context.Jobs;
using Aion.Modules;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Context.Services.Commands;

public interface ISynchronizeWorkflow
{
    // https://www.quartz-scheduler.net/documentation/quartz-3.x/quick-start.html

    Task<SynchronizeWorkflowResult?> Try(Workflow workflow);
}

// util: This class supports the API response.
public record SynchronizeWorkflowResult(string WorkflowName, Type ActionType)
{
    public DateTimeOffset? NextUtc { get; init; }

    public Exception? Exception { get; init; }
}

public record SynchronizeWorkflowResult<T>(string WorkflowName)
    : SynchronizeWorkflowResult(WorkflowName, typeof(T)) where T : ISynchronizeWorkflow;

public class ScheduleCustomWorkflow
(
    ILogger<ScheduleCustomWorkflow> logger,
    ISchedulerFactory schedulerFactory
) : ISynchronizeWorkflow
{
    public async Task<SynchronizeWorkflowResult?> Try(Workflow workflow)
    {
        var hasCustomTrigger = workflow.Trigger is not ICronTrigger;
        if (!hasCustomTrigger)
        {
            return null;
        }

        var jobDetail =
            JobBuilder
                .Create<WorkflowJob>()
                .WithIdentity(workflow.Trigger.JobKey)
                .Build();

        var scheduler = await schedulerFactory.GetScheduler();

        // core: Ensure the job is not already scheduled.
        if (await scheduler.CheckExists(jobDetail.Key))
        {
            if (await scheduler.DeleteJob(workflow.Trigger.JobKey))
            {
                logger.LogInformation("Workflow '{WorkflowName}' was unscheduled.", workflow.Name);
            }
        }

        logger.LogInformation("Workflow '{WorkflowName}' will be executed once at '{Next}'.", workflow.Name, workflow.Trigger.GetNextFireTimeUtc());

        var next = await scheduler.ScheduleJob(jobDetail, workflow.Trigger);
        return new SynchronizeWorkflowResult<ScheduleCustomWorkflow>(workflow.Name)
        {
            NextUtc = next
        };
    }
}

public class UnscheduleDisabledWorkflow
(
    ILogger<UnscheduleDisabledWorkflow> logger,
    ISchedulerFactory schedulerFactory
) : ISynchronizeWorkflow
{
    public async Task<SynchronizeWorkflowResult?> Try(Workflow workflow)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        var canUnscheduleDisabled = !workflow.Enabled && await scheduler.CheckExists(workflow.Trigger.JobKey);

        if (canUnscheduleDisabled)
        {
            if (await scheduler.DeleteJob(workflow.Trigger.JobKey))
            {
                logger.LogInformation("Workflow '{WorkflowName}' was unscheduled.", workflow.Name);
                return new SynchronizeWorkflowResult<UnscheduleDisabledWorkflow>(workflow.Name);
            }

            logger.LogWarning("Workflow '{WorkflowName}' could not be unscheduled.", workflow.Name);
        }

        return null;
    }
}

public class UnscheduleEmptyWorkflow
(
    ILogger<UnscheduleEmptyWorkflow> logger,
    ISchedulerFactory schedulerFactory
) : ISynchronizeWorkflow
{
    public async Task<SynchronizeWorkflowResult?> Try(Workflow workflow)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        var canUnscheduleEmpty = workflow.Enabled && !workflow.Steps.Any();

        if (canUnscheduleEmpty)
        {
            if (await scheduler.DeleteJob(workflow.Trigger.JobKey))
            {
                logger.LogInformation("Workflow '{WorkflowName}' was unscheduled.", workflow.Name);
                return new SynchronizeWorkflowResult<UnscheduleEmptyWorkflow>(workflow.Name);
            }
            logger.LogWarning("Workflow '{WorkflowName}' could not be unscheduled.", workflow.Name);
        }

        return null;
    }
}

public class RescheduleChangedWorkflow
(
    ILogger<RescheduleChangedWorkflow> logger,
    ISchedulerFactory schedulerFactory
) : ISynchronizeWorkflow
{
    public async Task<SynchronizeWorkflowResult?> Try(Workflow workflow)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        var canReschedule = workflow.Enabled && workflow.Steps.Any(s => s.Enabled);

        if (canReschedule)
        {
            // core: Compare the cron expressions.
            if (await scheduler.GetTrigger(workflow.Trigger.Key) is ICronTrigger { CronExpressionString: { } currentCron })
            {
                if (workflow.Trigger is ICronTrigger { CronExpressionString: { } otherCron } && !currentCron.Equals(otherCron))
                {
                    var next = await scheduler.RescheduleJob(workflow.Trigger.Key, workflow.Trigger);
                    logger.LogInformation("Workflow '{WorkflowName}' will be executed by {Cron} at '{Next}'.", workflow.Name, otherCron, next);
                    return new SynchronizeWorkflowResult<RescheduleChangedWorkflow>(workflow.Name)
                    {
                        NextUtc = next
                    };
                }
            }
        }

        return null;
    }
}

public class ScheduleNewWorkflow
(
    ILogger<ScheduleNewWorkflow> logger,
    ISchedulerFactory schedulerFactory
) : ISynchronizeWorkflow
{
    public async Task<SynchronizeWorkflowResult?> Try(Workflow workflow)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        var canScheduleNew =
            workflow.Enabled &&
            workflow.Steps.Any(s => s.Enabled) &&
            !await scheduler.CheckExists(workflow.Trigger.JobKey);

        if (canScheduleNew)
        {
            var jobDetail =
                JobBuilder
                    .Create<WorkflowJob>()
                    .WithIdentity(workflow.Trigger.JobKey)
                    .DisallowConcurrentExecution()
                    .Build();

            var next = await scheduler.ScheduleJob(jobDetail, workflow.Trigger);
            logger.LogInformation("Workflow '{WorkflowName}' will be executed by {Cron} at '{Next}'.", workflow.Name, ((ICronTrigger)workflow.Trigger).CronExpressionString, next);

            return new SynchronizeWorkflowResult<ScheduleNewWorkflow>(workflow.Name)
            {
                NextUtc = next
            };
        }

        return null;
    }
}

public class IgnoreWorkflow
(
    ILogger<IgnoreWorkflow> logger
) : ISynchronizeWorkflow
{
    public Task<SynchronizeWorkflowResult?> Try(Workflow workflow)
    {
        logger.LogInformation("Workflow '{WorkflowName}' is ignored.", workflow.Name);
        return Task.FromResult<SynchronizeWorkflowResult>(new SynchronizeWorkflowResult<IgnoreWorkflow>(workflow.Name))!;
    }
}