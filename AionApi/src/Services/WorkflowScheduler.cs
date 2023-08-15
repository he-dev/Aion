using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AionApi.Models;
using AionApi.Utilities;
using Quartz;
using Quartz.Impl.Matchers;
using Quartz.Impl.Triggers;
using Reusable.Extensions;
using Reusable.Wiretap;
using Reusable.Wiretap.Abstractions;
using Reusable.Wiretap.Data;
using Reusable.Wiretap.Extensions;

namespace AionApi.Services;

public class WorkflowScheduler
{
    public WorkflowScheduler(ILogger<WorkflowScheduler> logger, ISchedulerFactory schedulerFactory)
    {
        Logger = logger;
        SchedulerFactory = schedulerFactory;
    }

    private ILogger Logger { get; }

    private ISchedulerFactory SchedulerFactory { get; }

    /// <summary>
    /// Schedules a new job or updates the schedule of an existing one.
    /// </summary>
    public async Task<DateTimeOffset?> Schedule(Workflow workflow)
    {
        using var activity = Logger.Begin("ScheduleWorkflow").LogArgs(details: new { workflow = workflow.Name });

        var scheduler = await SchedulerFactory.GetScheduler();

        try
        {
            if (!workflow)
            {
                await scheduler.DeleteJob(workflow.JobKey);
                activity.LogBreak(message: "Workflow is disabled.");
                return default;
            }

            if (workflow.IsEmpty)
            {
                await scheduler.DeleteJob(workflow.JobKey);
                activity.LogBreak(message: "Workflow has no enabled commands.");
                return default;
            }

            var trigger = workflow.CronTrigger;

            if (await scheduler.GetTrigger(trigger.Key) is CronTriggerImpl current)
            {
                if (current.CronExpressionString?.ToCronExpression().Equals(workflow.Schedule.ToCronExpression()) is true)
                {
                    activity.LogNoop(message: "Workflow schedule has not changed.");
                    return default;
                }

                if (await scheduler.RescheduleJob(trigger.Key, trigger) is { } next)
                {
                    activity.LogInfo(details: new { schedule = new { previous = current.CronExpressionString, current = workflow.Schedule } });
                    activity.LogResult(details: new { next });
                    activity.LogEnd(message: "Workflow rescheduled.");
                    return next;
                }
            }
            else
            {
                var jobDetail = workflow.JobBuilder.Build();
                var next = await scheduler.ScheduleJob(jobDetail, trigger);
                activity.LogResult(details: new { next });
                activity.LogEnd();
                return next;
            }
        }
        catch (Exception ex)
        {
            activity.Items.Exception(ex);
        }

        return default;
    }

    public async Task<bool> Delete(string name)
    {
        using var activity = Logger.Begin("DeleteJob").LogArgs(details: new { name });
        var scheduler = await SchedulerFactory.GetScheduler();
        var deleted = await scheduler.DeleteJob(new Workflow { Name = name }.JobKey);
        try
        {
            return deleted;
        }
        finally
        {
            if (deleted)
            {
                activity.LogNoop(message: "Job does not exist.");
            }
            else
            {
                activity.LogEnd();
            }
        }
    }

    public async IAsyncEnumerable<ICronTrigger> EnumerateTriggers(string group)
    {
        var scheduler = await SchedulerFactory.GetScheduler();
        var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(group));
        foreach (var jobKey in jobKeys)
        {
            await foreach (var trigger in EnumerateTriggers(jobKey))
            {
                yield return trigger;
            }
        }
    }

    public async IAsyncEnumerable<ICronTrigger> EnumerateTriggers(JobKey jobKey)
    {
        var scheduler = await SchedulerFactory.GetScheduler();
        foreach (var trigger in (await scheduler.GetTriggersOfJob(jobKey)).Cast<ICronTrigger>())
        {
            yield return trigger;
        }
    }

    public async IAsyncEnumerable<ScheduleInfo> EnumerateNext(JobKey jobKey, DateTimeOffset afterTimeUtc)
    {
        if (await EnumerateTriggers(jobKey).SingleOrDefaultAsync() is { CronExpressionString: { } cronExpressionString })
        {
            var schedules =
                cronExpressionString
                    .ToCronExpression()
                    .Generate(first: cron => cron.GetTimeAfter(afterTimeUtc), next: (cron, previous) => cron.GetTimeAfter(previous!.Value))
                    .Cast<DateTimeOffset>()
                    .Select(next => new ScheduleInfo(next, next - afterTimeUtc));

            foreach (var schedule in schedules)
            {
                yield return schedule;
            }
        }
    }
}

public record ScheduleInfo(DateTimeOffset Next, TimeSpan Wait);