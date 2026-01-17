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
    Task<SynchronizeWorkflowResult?> Try(Workflow workflow);
}

// util: This class supports the API response.
public record SynchronizeWorkflowResult
{
    public required string WorkflowName { get; init; }

    public required Type ActionType { get; init; }

    public DateTimeOffset? NextUtc { get; init; }
}

public class ScheduleCustomWorkflow
(
    ILogger<ScheduleCustomWorkflow> logger,
    ISchedulerFactory schedulerFactory
) : ISynchronizeWorkflow
{
    public async Task<SynchronizeWorkflowResult?> Try(Workflow workflow)
    {
        if (workflow.Trigger is ICronTrigger)
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
        return new SynchronizeWorkflowResult
        {
            WorkflowName = workflow.Name,
            ActionType = typeof(ScheduleCustomWorkflow),
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

        if (!workflow.Enabled)
        {
            if (await scheduler.CheckExists(workflow.Trigger.JobKey))
            {
                if (await scheduler.DeleteJob(workflow.Trigger.JobKey))
                {
                    logger.LogInformation("Workflow '{WorkflowName}' was unscheduled.", workflow.Name);
                }
            }

            return new SynchronizeWorkflowResult
            {
                WorkflowName = workflow.Name,
                ActionType = typeof(UnscheduleDisabledWorkflow),
            };
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

        if (!workflow.Steps.Any(s => s.Enabled))
        {
            if (await scheduler.CheckExists(workflow.Trigger.JobKey))
            {
                if (await scheduler.DeleteJob(workflow.Trigger.JobKey))
                {
                    logger.LogInformation("Workflow '{WorkflowName}' was unscheduled.", workflow.Name);
                }
            }

            return new SynchronizeWorkflowResult
            {
                WorkflowName = workflow.Name,
                ActionType = typeof(UnscheduleEmptyWorkflow),
            };
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

        if (await scheduler.CheckExists(workflow.Trigger.JobKey))
        {
            if (await scheduler.GetTrigger(workflow.Trigger.Key) is ICronTrigger { CronExpressionString: { } currentCron })
            {
                if (workflow.Trigger is ICronTrigger { CronExpressionString: { } otherCron } && !currentCron.Equals(otherCron))
                {
                    var next = await scheduler.RescheduleJob(workflow.Trigger.Key, workflow.Trigger);
                    return new SynchronizeWorkflowResult
                    {
                        WorkflowName = workflow.Name,
                        ActionType = typeof(RescheduleChangedWorkflow),
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

        if (await scheduler.CheckExists(workflow.Trigger.JobKey))
        {
            var jobDetail =
                JobBuilder
                    .Create<WorkflowJob>()
                    .WithIdentity(workflow.Trigger.JobKey)
                    .DisallowConcurrentExecution()
                    .Build();

            var next = await scheduler.ScheduleJob(jobDetail, workflow.Trigger);

            return new SynchronizeWorkflowResult
            {
                WorkflowName = workflow.Name,
                ActionType = typeof(ScheduleNewWorkflow),
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
        return Task.FromResult(new SynchronizeWorkflowResult
        {
            WorkflowName = workflow.Name,
            ActionType = typeof(ScheduleNewWorkflow),
        })!;
    }
}