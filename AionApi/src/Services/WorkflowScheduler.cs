using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AionApi.Jobs;
using AionApi.Models;
using Quartz;
using Quartz.Impl.Matchers;
using Quartz.Impl.Triggers;
using Reusable.Wiretap;
using Reusable.Wiretap.Abstractions;

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
        using var status = Logger.Start("ScheduleWorkflow", new { name = workflow.Name, workflow.Schedule });

        var scheduler = await SchedulerFactory.GetScheduler();

        try
        {
            if (!workflow)
            {
                var deleted = await scheduler.DeleteJob(new JobKey(workflow.Name, nameof(Workflow)));
                status.Canceled(new { reason = "Workflow is disabled.", deleted });
                return default;
            }

            var trigger = workflow.ToCronTrigger();

            if (await scheduler.GetTrigger(trigger.Key) is CronTriggerImpl current && current.CronExpressionString == workflow.Schedule)
            {
                status.Canceled(new { reason = "Workflow schedule did not change." });
                return current.GetFireTimeAfter(DateTimeOffset.UtcNow);
            }

            if (await scheduler.RescheduleJob(trigger.Key, trigger) is { } next)
            {
                status.Canceled(new { reason = "Workflow rescheduled." });
                return next;
            }

            return await scheduler.ScheduleJob(workflow.ToJobDetail<WorkflowRunner>(), trigger);
        }
        catch (Exception ex)
        {
            status.Exception(ex);
        }

        return default;
    }

    public async Task<bool> Delete(string workflow)
    {
        using var status = Logger.Start("DeleteJob", new { name = workflow });
        var scheduler = await SchedulerFactory.GetScheduler();
        if (await scheduler.DeleteJob(new JobKey(workflow, nameof(Workflow))))
        {
            status.Completed();
            return true;
        }

        status.Canceled(new { reason = "Job not found." });
        return false;
    }

    public async IAsyncEnumerable<ICronTrigger> EnumerateActiveWorkflowCronTriggers()
    {
        var scheduler = await SchedulerFactory.GetScheduler();
        var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(nameof(Workflow)));
        foreach (var jobKey in jobKeys)
        {
            if (await scheduler.GetTriggersOfJob(jobKey).ContinueWith(x => x.Result.Single()) is ICronTrigger trigger)
            {
                yield return trigger;
            }
        }
    }
}