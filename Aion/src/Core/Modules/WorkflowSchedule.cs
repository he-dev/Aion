using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Head;
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
    public async Task<(SynchronizationAction Action, DateTimeOffset? Next)> Synchronize(Workflow workflow)
    {
        logger.LogInformation("Workflow '{workflow}' is being synchronized...", workflow.Name);

        var scheduler = await schedulerFactory.GetScheduler();

        try
        {
            if (!workflow.Enabled)
            {
                if (await scheduler.DeleteJob(workflow.JobKey))
                {
                    logger.LogInformation("Workflow '{workflow}' deleted from schedule because it is disabled.", workflow.Name);
                    return (SynchronizationAction.Deleted, null);
                }

                logger.LogInformation("Workflow '{workflow}' skipped because it is disabled.", workflow.Name);
                return (SynchronizationAction.Skipped, null);
            }

            if (!workflow.Steps.Any(s => s.Enabled))
            {
                if (await scheduler.DeleteJob(workflow.JobKey))
                {
                    logger.LogInformation("Workflow '{workflow}' deleted from schedule because it has no enabled steps.", workflow.Name);
                    return (SynchronizationAction.Deleted, null);
                }

                logger.LogInformation("Workflow '{workflow}' skipped because it has no enabled steps.", workflow.Name);
                return (SynchronizationAction.Skipped, null);
            }

            // .. This might throw when the Cron property is invalid.
            var trigger = workflow.Trigger;

            if (await scheduler.GetTrigger(trigger.Key) is ICronTrigger { CronExpressionString: { } cron } current)
            {
                if (cron.Equals(trigger.CronExpressionString))
                {
                    logger.LogInformation("Workflow '{workflow}' skipped because it is already scheduled.", workflow.Name);
                    return (SynchronizationAction.Skipped, null);
                }

                if (await scheduler.RescheduleJob(trigger.Key, trigger) is { } next)
                {
                    logger.LogInformation("Workflow '{workflow}' rescheduled for '{next}'.", workflow.Name, next);
                    return (SynchronizationAction.Updated, null);
                }

                logger.LogInformation("Workflow '{workflow}' skipped because it could not be rescheduled.", workflow.Name);
                return (SynchronizationAction.Skipped, null);
            }
            else
            {
                var next = await Schedule(workflow.Path, workflow.Name, workflow.Trigger);
                logger.LogInformation("Workflow '{workflow}' scheduled for '{next}'.", workflow.Name, next);
                return (SynchronizationAction.Created, null);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow '{workflow}' could not be synchronized.", workflow.Name);
            throw;
        }
    }

    public async Task<DateTimeOffset> StartNow(Workflow workflow)
    {
        if (workflow.Steps.All(s => !s.Enabled))
        {
            logger.LogWarning("Workflow '{workflow}' has no enabled steps.", workflow.Name);
        }

        var scheduler = await schedulerFactory.GetScheduler();

        var job =
            JobBuilder
                .Create<Jobs.AdHocWorkflowJob>()
                .WithIdentity(workflow.Name, JobGroupNames.Workflows)
                .UsingJobData(nameof(Workflow.Path), workflow.Path)
                .Build();

        var trigger =
            TriggerBuilder
                .Create()
                .WithIdentity(workflow.Name, JobGroupNames.Workflows)
                .StartNow()
                .WithSimpleSchedule(x => x.WithRepeatCount(0))
                .UsingJobData("start", "now")
                .Build();

        return await scheduler.ScheduleJob(job, trigger);
    }

    public async Task<DateTimeOffset> StartAt(Workflow workflow, DateTimeOffset startAt)
    {
        if (workflow.Steps.All(s => !s.Enabled))
        {
            logger.LogWarning("Workflow '{workflow}' has no enabled steps.", workflow.Name);
        }

        var scheduler = await schedulerFactory.GetScheduler();

        var job =
            JobBuilder
                .Create<Jobs.AdHocWorkflowJob>()
                .WithIdentity(workflow.Name, JobGroupNames.Workflows)
                .UsingJobData(nameof(Workflow.Path), workflow.Path)
                .Build();

        var trigger =
            TriggerBuilder
                .Create()
                .WithIdentity(workflow.Name, JobGroupNames.Workflows)
                .StartAt(startAt)
                .WithSimpleSchedule(x => x.WithRepeatCount(0))
                .UsingJobData("start", "at")
                .Build();

        //logger.LogDebug("Workflow '{name}' will be started in {delay} seconds.'", workflow.Name, startAt);
        return await scheduler.ScheduleJob(job, trigger);
    }

    public async Task<DateTimeOffset> StartIn(Workflow workflow, TimeSpan delay)
    {
        return await StartAt(workflow, DateTimeOffset.UtcNow + delay);
    }

    public async Task<DateTimeOffset> Schedule(string path, string name, ITrigger trigger)
    {
        var jobDetail =
            JobBuilder
                .Create<Jobs.RegularWorkflowJob>()
                .WithIdentity(name, JobGroupNames.Workflows)
                .UsingJobData("Path", path)
                .Build();

        var scheduler = await schedulerFactory.GetScheduler();
        return await scheduler.ScheduleJob(jobDetail, trigger);
    }

    public async Task<bool> Delete(string name)
    {
        //using var activity = Logger.Begin("DeleteJob").LogArgs(details: new { name });
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

    public enum SynchronizationAction
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