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

namespace AionApi.Services;

public class WorkflowScheduler
{
    public WorkflowScheduler(ILogger logger, ISchedulerFactory schedulerFactory)
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
        using var activity = Logger.Begin("ScheduleWorkflow", details: new { name = workflow.Name });

        var scheduler = await SchedulerFactory.GetScheduler();

        try
        {
            if (!workflow)
            {
                await scheduler.DeleteJob(workflow.JobKey);
                activity.LogStop(message: "Workflow is disabled.");
                return default;
            }

            if (workflow.IsEmpty)
            {
                await scheduler.DeleteJob(workflow.JobKey);
                activity.LogStop(message: "Workflow has no enabled commands.");
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
                    activity.LogEnd(message: "Workflow rescheduled.", details: new { schedule = new { previous = current.CronExpressionString, current = workflow.Schedule, next } });
                    return next;
                }
            }

            var jobDetail = workflow.JobBuilder.Build();
            return await scheduler.ScheduleJob(jobDetail, trigger);
        }
        catch (Exception ex)
        {
            activity.Items.Exception(ex);
        }

        return default;
    }

    public async Task<bool> Delete(string name)
    {
        using var status = Logger.Begin("DeleteJob", details: new { name });
        var scheduler = await SchedulerFactory.GetScheduler();
        var deleted = await scheduler.DeleteJob(new Workflow { Name = name }.JobKey);
        try
        {
            return deleted;
        }
        finally
        {
            status.LogEnd(details: new { deleted });
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