using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aion.Home;
using Aion.Home.Jobs;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

namespace Aion.Core.Modules;

// https://www.quartz-scheduler.net/documentation/quartz-3.x/quick-start.html

// role: This class provides convenient scheduling methods to other modules.
public class WorkflowScheduler
(
    ILogger<WorkflowScheduler> logger,
    ISchedulerFactory schedulerFactory
)
{
    public async Task<(SynchronizationResult Action, DateTimeOffset? Next)> Synchronize(Workflow workflow)
    {
        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflow.Name });

        var scheduler = await schedulerFactory.GetScheduler();

        if (!workflow.IsOn)
        {
            if (await scheduler.DeleteJob(workflow.JobKey))
            {
                logger.LogInformation("Workflow no longer enabled: {SynchronizationResult}.", SynchronizationResult.Delete);
                return (SynchronizationResult.Delete, null);
            }

            logger.LogInformation("Workflow is disabled: {SynchronizationResult}.", SynchronizationResult.Ignore);
            return (SynchronizationResult.Ignore, null);
        }

        if (!workflow.Steps.Any(s => s.IsOn))
        {
            if (await scheduler.DeleteJob(workflow.JobKey))
            {
                logger.LogInformation("Workflow no longer has any enabled steps: {SynchronizationResult}.", SynchronizationResult.Delete);
                return (SynchronizationResult.Delete, null);
            }

            logger.LogInformation("Workflow has no enabled steps: {SynchronizationResult}.", SynchronizationResult.Ignore);
            ;
            return (SynchronizationResult.Ignore, null);
        }

        // .. This might throw when the Cron property is invalid.
        var trigger = workflow.Trigger;

        if (await scheduler.GetTrigger(trigger.Key) is ICronTrigger { CronExpressionString: { } cron } current)
        {
            if (cron.Equals(trigger.CronExpressionString))
            {
                logger.LogInformation("Workflow is already scheduled: {SynchronizationResult}.", SynchronizationResult.Ignore);
                return (SynchronizationResult.Ignore, null);
            }

            if (await scheduler.RescheduleJob(trigger.Key, trigger) is { } next)
            {
                logger.LogInformation("Workflow schedule has changed: {SynchronizationResult}. Next execution at '{Next}'.", SynchronizationResult.Update, next);
                return (SynchronizationResult.Update, null);
            }

            logger.LogInformation("Workflow could not be rescheduled: {SynchronizationResult}.", SynchronizationResult.Ignore);
            return (SynchronizationResult.Ignore, null);
        }
        else
        {
            var next = await Schedule(workflow);
            logger.LogInformation("Workflow is new: {SynchronizationResult}. Next execution at '{Next}'.", SynchronizationResult.Create, next);
            return (SynchronizationResult.Create, null);
        }
    }

    public async Task<DateTimeOffset> StartNow(Workflow workflow)
    {
        var job =
            JobBuilder
                .Create<OnDemandWorkflowJob>()
                .WithIdentity(workflow.Name, JobGroupNames.Workflows)
                .UsingJobData(nameof(Workflow.Path), workflow.Path)
                .Build();

        await EnsureWorkflowNotScheduled(job);

        var trigger =
            TriggerBuilder
                .Create()
                .WithIdentity(workflow.Name, JobGroupNames.Workflows)
                .StartNow()
                .WithSimpleSchedule(x => x.WithRepeatCount(0))
                .UsingJobData(nameof(WorkflowTriggerGroup), nameof(WorkflowTriggerGroup.StartNow))
                .Build();

        var scheduler = await schedulerFactory.GetScheduler();
        return await scheduler.ScheduleJob(job, trigger);
    }

    public async Task<DateTimeOffset> StartAt(Workflow workflow, DateTimeOffset startAt)
    {
        var job =
            JobBuilder
                .Create<OnDemandWorkflowJob>()
                .WithIdentity(workflow.Name, JobGroupNames.Workflows)
                .UsingJobData(nameof(Workflow.Path), workflow.Path)
                .Build();

        await EnsureWorkflowNotScheduled(job);

        var trigger =
            TriggerBuilder
                .Create()
                .WithIdentity(workflow.Name, JobGroupNames.Workflows)
                .StartAt(startAt)
                .WithSimpleSchedule(x => x.WithRepeatCount(0))
                .UsingJobData(nameof(WorkflowTriggerGroup), nameof(WorkflowTriggerGroup.StartAt))
                .Build();

        var scheduler = await schedulerFactory.GetScheduler();
        return await scheduler.ScheduleJob(job, trigger);
    }

    public async Task<DateTimeOffset> StartIn(Workflow workflow, TimeSpan delay)
    {
        var job =
            JobBuilder
                .Create<OnDemandWorkflowJob>()
                .WithIdentity(workflow.Name, JobGroupNames.Workflows)
                .UsingJobData(nameof(Workflow.Path), workflow.Path)
                .Build();

        await EnsureWorkflowNotScheduled(job);

        var trigger =
            TriggerBuilder
                .Create()
                .WithIdentity(workflow.Name, JobGroupNames.Workflows)
                .StartAt(DateTimeOffset.UtcNow + delay)
                .WithSimpleSchedule(x => x.WithRepeatCount(0))
                .UsingJobData(nameof(WorkflowTriggerGroup), nameof(WorkflowTriggerGroup.StartIn))
                .Build();

        var scheduler = await schedulerFactory.GetScheduler();
        return await scheduler.ScheduleJob(job, trigger);
    }

    public async Task<DateTimeOffset> Schedule(Workflow workflow)
    {
        var jobDetail =
            JobBuilder
                .Create<RegularWorkflowJob>()
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
        return await scheduler.DeleteJob(jobKey);
    }

    private async Task EnsureWorkflowNotScheduled(IJobDetail jobDetail)
    {
        var scheduler = await schedulerFactory.GetScheduler();
        if (await scheduler.CheckExists(jobDetail.Key))
        {
            throw new WorkflowAlreadyScheduledException();
        }
    }

    public enum SynchronizationResult
    {
        Ignore,
        Create,
        Update,
        Delete
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

public enum WorkflowTriggerGroup
{
    Cron,
    StartNow,
    StartIn,
    StartAt
}

public class WorkflowAlreadyScheduledException : Exception;