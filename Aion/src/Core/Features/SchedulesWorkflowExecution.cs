using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Home.Jobs;
using Aion.Util.Quartz;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Features;

// https://www.quartz-scheduler.net/documentation/quartz-3.x/quick-start.html

public class SchedulesWorkflowExecution
(
    ILogger<SchedulesWorkflowExecution> logger,
    ISchedulerFactory schedulerFactory
)
{
    public async Task<ProfileSynchronization> For(Workflow workflow, string profile)
    {
        var scheduler = await schedulerFactory.GetScheduler();
        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflow.Name });

        if (!workflow.IsOn)
        {
            if (await scheduler.DeleteJob(workflow.CreatesJobKey(profile)))
            {
                logger.LogInformation("Workflow no longer enabled: {SynchronizationResult}.", SynchronizationResult.Delete);
                return new ProfileSynchronization(SynchronizationResult.Delete);
            }

            logger.LogInformation("Workflow is disabled: {SynchronizationResult}.", SynchronizationResult.Ignore);
            return new ProfileSynchronization(SynchronizationResult.Ignore);
        }

        if (!workflow.Steps.Any(s => s.IsOn))
        {
            if (await scheduler.DeleteJob(workflow.CreatesJobKey(profile)))
            {
                logger.LogInformation("Workflow no longer has any enabled steps: {SynchronizationResult}.", SynchronizationResult.Delete);
                return new ProfileSynchronization(SynchronizationResult.Delete);
            }

            logger.LogInformation("Workflow has no enabled steps: {SynchronizationResult}.", SynchronizationResult.Ignore);
            return new ProfileSynchronization(SynchronizationResult.Ignore);
        }

        // .. This might throw when the Cron property is invalid.
        var trigger = workflow.CreatesCronTrigger(profile);

        if (await scheduler.GetTrigger(trigger.Key) is ICronTrigger { CronExpressionString: { } cron } current)
        {
            if (cron.Equals(trigger.CronExpressionString))
            {
                logger.LogInformation("Workflow is already scheduled: {SynchronizationResult}.", SynchronizationResult.Ignore);
                return new ProfileSynchronization(SynchronizationResult.Ignore);
            }

            if (await scheduler.RescheduleJob(trigger.Key, trigger) is { } next)
            {
                logger.LogInformation("Workflow schedule has changed: {SynchronizationResult}. Next execution at '{Next}'.", SynchronizationResult.Update, next);
                return new ProfileSynchronization(SynchronizationResult.Update);
            }

            logger.LogInformation("Workflow could not be rescheduled: {SynchronizationResult}.", SynchronizationResult.Ignore);
            return new ProfileSynchronization(SynchronizationResult.Ignore);
        }
        else
        {
            var next = await SchedulesJob(workflow, profile);
            logger.LogInformation("Workflow is new: {SynchronizationResult}. Next execution at '{Next}'.", SynchronizationResult.Create, next);
            return new ProfileSynchronization(SynchronizationResult.Create);
        }
    }

    public async Task<DateTimeOffset> Now(Workflow workflow)
    {
        var job =
            JobBuilder
                .Create<ExecutesWorkflowOnDemand>()
                .WithIdentity(workflow.Name, new GroupName<ExecutesWorkflowOnDemand>(""))
                .UsingJobData(nameof(Workflow.Path), workflow.Path)
                .Build();

        await EnsureWorkflowNotScheduled(job);

        var trigger =
            TriggerBuilder
                .Create()
                .WithIdentity(workflow.Name, new GroupName<ExecutesWorkflowOnDemand>())
                .StartNow()
                .WithSimpleSchedule(x => x.WithRepeatCount(0))
                .UsingJobData(WorkflowTriggerGroup.StartNow)
                .Build();

        var scheduler = await schedulerFactory.GetScheduler();
        return await scheduler.ScheduleJob(job, trigger);
    }

    public async Task<DateTimeOffset> At(Workflow workflow, DateTimeOffset startAt)
    {
        var job =
            JobBuilder
                .Create<ExecutesWorkflowOnDemand>()
                .WithIdentity(workflow.Name, new GroupName<ExecutesWorkflowOnDemand>())
                .UsingJobData(nameof(Workflow.Path), workflow.Path)
                .Build();

        await EnsureWorkflowNotScheduled(job);

        var trigger =
            TriggerBuilder
                .Create()
                .WithIdentity(workflow.Name, new GroupName<ExecutesWorkflowOnDemand>())
                .StartAt(startAt)
                .WithSimpleSchedule(x => x.WithRepeatCount(0))
                .UsingJobData(WorkflowTriggerGroup.StartAt)
                .Build();

        var scheduler = await schedulerFactory.GetScheduler();
        return await scheduler.ScheduleJob(job, trigger);
    }

    public async Task<DateTimeOffset> In(Workflow workflow, TimeSpan delay)
    {
        var job =
            JobBuilder
                .Create<ExecutesWorkflowOnDemand>()
                .WithIdentity(workflow.Name, new GroupName<ExecutesWorkflowOnDemand>())
                .UsingJobData(nameof(Workflow.Path), workflow.Path)
                .Build();

        await EnsureWorkflowNotScheduled(job);

        var trigger =
            TriggerBuilder
                .Create()
                .WithIdentity(workflow.Name, new GroupName<ExecutesWorkflowOnDemand>())
                .StartAt(DateTimeOffset.UtcNow + delay)
                .WithSimpleSchedule(x => x.WithRepeatCount(0))
                .UsingJobData(WorkflowTriggerGroup.StartIn)
                .Build();

        var scheduler = await schedulerFactory.GetScheduler();
        return await scheduler.ScheduleJob(job, trigger);
    }

    private async Task<DateTimeOffset> SchedulesJob(Workflow workflow, string profile)
    {
        var jobDetail =
            JobBuilder
                .Create<ExecutesWorkflowOnSchedule>()
                .WithIdentity(workflow.Name, new GroupName<ExecutesWorkflowOnDemand>())
                .UsingJobData(nameof(Workflow.Path), workflow.Path)
                .Build();

        var scheduler = await schedulerFactory.GetScheduler();
        return await scheduler.ScheduleJob(jobDetail, workflow.CreatesCronTrigger(profile));
    }

    public async Task<bool> NoMore(JobKey jobKey)
    {
        var scheduler = await schedulerFactory.GetScheduler();
        //var jobKey = new JobKey(name, new GroupName<ExecutesWorkflowOnDemand>());
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
}

public enum SynchronizationResult
{
    Ignore,
    Create,
    Update,
    Delete
}

public record ProfileSynchronization(SynchronizationResult Result, DateTimeOffset? Next = null);

public enum WorkflowTriggerGroup
{
    Cron,
    StartNow,
    StartIn,
    StartAt
}

public class WorkflowAlreadyScheduledException : Exception;