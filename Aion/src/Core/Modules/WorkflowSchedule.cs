using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Home;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

namespace Aion.Core.Modules;

// https://www.quartz-scheduler.net/documentation/quartz-3.x/quick-start.html

// role: This class provides convenient scheduling methods to other modules.
public class WorkflowSchedule
(
    ILogger<WorkflowSchedule> logger,
    ISchedulerFactory schedulerFactory
)
{
    public async Task<(SynchronizationResult Action, DateTimeOffset? Next)> Synchronize(Workflow workflow)
    {
        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflow.Name});
        logger.LogInformation("Workflow is being synchronized...");

        var scheduler = await schedulerFactory.GetScheduler();

        try
        {
            if (!workflow.Enabled)
            {
                if (await scheduler.DeleteJob(workflow.JobKey))
                {
                    logger.LogInformation("Workflow {SynchronizationResult} from schedule because it is disabled.", SynchronizationResult.Deleted);
                    return (SynchronizationResult.Deleted, null);
                }

                logger.LogInformation("Workflow {SynchronizationResult} because it is disabled.", SynchronizationResult.Skipped);
                return (SynchronizationResult.Skipped, null);
            }

            if (!workflow.Steps.Any(s => s.Enabled))
            {
                if (await scheduler.DeleteJob(workflow.JobKey))
                {
                    logger.LogInformation("Workflow {SynchronizationResult} from schedule because it has no enabled steps.", SynchronizationResult.Deleted);
                    return (SynchronizationResult.Deleted, null);
                }

                logger.LogInformation("Workflow {SynchronizationResult} because it has no enabled steps.", SynchronizationResult.Skipped);;
                return (SynchronizationResult.Skipped, null);
            }

            // .. This might throw when the Cron property is invalid.
            var trigger = workflow.Trigger;

            if (await scheduler.GetTrigger(trigger.Key) is ICronTrigger { CronExpressionString: { } cron } current)
            {
                if (cron.Equals(trigger.CronExpressionString))
                {
                    logger.LogInformation("Workflow {SynchronizationResult} because it is already scheduled.", SynchronizationResult.Skipped);
                    return (SynchronizationResult.Skipped, null);
                }

                if (await scheduler.RescheduleJob(trigger.Key, trigger) is { } next)
                {
                    logger.LogInformation("Workflow {SynchronizationResult}. Next execution at '{Next}'.", SynchronizationResult.Updated, next);
                    return (SynchronizationResult.Updated, null);
                }

                logger.LogInformation("Workflow {SynchronizationResult} because it could not be rescheduled.", SynchronizationResult.Skipped);
                return (SynchronizationResult.Skipped, null);
            }
            else
            {
                var next = await Schedule(workflow);
                logger.LogInformation("Workflow {SynchronizationResult}. Next execution at '{Next}'.", SynchronizationResult.Created, next);
                return (SynchronizationResult.Created, null);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow could not be synchronized.");
            throw;
        }
    }

    public async Task<DateTimeOffset> StartNow(Workflow workflow)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        var job =
            JobBuilder
                .Create<Jobs.OnDemandWorkflowJob>()
                .WithIdentity(workflow.Name, JobGroupNames.Workflows)
                .UsingJobData(nameof(Workflow.Path), workflow.Path)
                .Build();

        var trigger =
            TriggerBuilder
                .Create()
                .WithIdentity(workflow.Name, JobGroupNames.Workflows)
                .StartNow()
                .WithSimpleSchedule(x => x.WithRepeatCount(0))
                .UsingJobData(nameof(OnDemandOption), nameof(OnDemandOption.StartNow))
                .Build();

        return await scheduler.ScheduleJob(job, trigger);
    }

    public async Task<DateTimeOffset> StartAt(Workflow workflow, DateTimeOffset startAt)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        var job =
            JobBuilder
                .Create<Jobs.OnDemandWorkflowJob>()
                .WithIdentity(workflow.Name, JobGroupNames.Workflows)
                .UsingJobData(nameof(Workflow.Path), workflow.Path)
                .Build();

        var trigger =
            TriggerBuilder
                .Create()
                .WithIdentity(workflow.Name, JobGroupNames.Workflows)
                .StartAt(startAt)
                .WithSimpleSchedule(x => x.WithRepeatCount(0))
                .UsingJobData(nameof(OnDemandOption), nameof(OnDemandOption.StartAt))
                .Build();

        return await scheduler.ScheduleJob(job, trigger);
    }

    public async Task<DateTimeOffset> StartIn(Workflow workflow, TimeSpan delay)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        var job =
            JobBuilder
                .Create<Jobs.OnDemandWorkflowJob>()
                .WithIdentity(workflow.Name, JobGroupNames.Workflows)
                .UsingJobData(nameof(Workflow.Path), workflow.Path)
                .Build();

        var trigger =
            TriggerBuilder
                .Create()
                .WithIdentity(workflow.Name, JobGroupNames.Workflows)
                .StartAt(DateTimeOffset.UtcNow + delay)
                .WithSimpleSchedule(x => x.WithRepeatCount(0))
                .UsingJobData(nameof(OnDemandOption), nameof(OnDemandOption.StartIn))
                .Build();

        return await scheduler.ScheduleJob(job, trigger);
    }

    public async Task<DateTimeOffset> Schedule(Workflow workflow)
    {
        var jobDetail =
            JobBuilder
                .Create<Jobs.RegularWorkflowJob>()
                .WithIdentity(workflow.Name, JobGroupNames.Workflows)
                .UsingJobData(nameof(Workflow.Path), workflow.Path)
                .Build();

        var scheduler = await schedulerFactory.GetScheduler();
        return await scheduler.ScheduleJob(jobDetail, workflow.Trigger);
    }

    public async Task<bool> Delete(string name)
    {
        var scheduler = await schedulerFactory.GetScheduler();
        var jobKey = new JobKey(name, JobGroupNames.Workflows);
        var deleted = await scheduler.DeleteJob(jobKey);
        try
        {
            return deleted;
        }
        finally
        {
            if (deleted)
            {
                //activity.LogNoop(message: "Job does not exist.");
            }
            else
            {
                //activity.LogEnd();
            }
        }
    }

    public enum SynchronizationResult
    {
        Skipped,
        Created,
        Updated,
        Deleted,
        Faulted
    }

    public class Collection
    (
        ILogger<Collection> logger,
        ISchedulerFactory schedulerFactory
    ) : IAsyncEnumerable<ITrigger>
    {
        public async IAsyncEnumerator<ITrigger> GetAsyncEnumerator(CancellationToken cancellationToken = new())
        {
            var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
            var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(JobGroupNames.Workflows), cancellationToken);
            foreach (var jobKey in jobKeys)
            {
                foreach (var trigger in await scheduler.GetTriggersOfJob(jobKey, cancellationToken))
                {
                    yield return trigger;
                }
            }
        }
    }
}

public enum OnDemandOption
{
    None,
    StartNow,
    StartIn,
    StartAt
}

public enum WorkflowTriggerType
{
    None,
    Cron,
    OnDemand
}