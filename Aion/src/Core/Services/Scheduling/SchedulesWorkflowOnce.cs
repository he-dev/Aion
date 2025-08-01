using System;
using System.Threading.Tasks;
using Aion.Home.Jobs;
using Aion.Util.Quartz;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Services.Scheduling;

// https://www.quartz-scheduler.net/documentation/quartz-3.x/quick-start.html

public class SchedulesWorkflowOnce
(
    ILogger<SchedulesWorkflowOnce> logger,
    ISchedulerFactory schedulerFactory
)
{
    public async Task<DateTimeOffset> ToStartNow(WorkflowMatch workflowMatch)
    {
        return await Schedule(workflowMatch, WorkflowStart.OnceNow, triggerBuilder => triggerBuilder.StartNow());
    }

    public async Task<DateTimeOffset> ToStartAt(WorkflowMatch workflowMatch, DateTimeOffset startAt)
    {
        return await Schedule(workflowMatch, WorkflowStart.OnceAt, triggerBuilder => triggerBuilder.StartAt(startAt));
    }

    public async Task<DateTimeOffset> ToStartIn(WorkflowMatch workflowMatch, TimeSpan delay)
    {
        return await Schedule(workflowMatch, WorkflowStart.OnceIn, triggerBuilder => triggerBuilder.StartAt(DateTimeOffset.UtcNow + delay));
    }

    private async Task<DateTimeOffset> Schedule(WorkflowMatch workflowMatch, WorkflowStart start, Func<TriggerBuilder, TriggerBuilder> customizesTrigger)
    {
        var jobBuilder =
            JobBuilder
                .Create<ExecutesWorkflowOnce>()
                .WithIdentity(workflowMatch.Name, JobGroupName.From<ExecutesWorkflowOnce>(workflowMatch.Profile.Name));

        var triggerBuilder =
            TriggerBuilder
                .Create()
                .WithIdentity(workflowMatch.Name, JobGroupName.From<ExecutesWorkflowOnce>(workflowMatch.Profile.Name))
                .UsingJobData(JobDataKeys.ProfileName, workflowMatch.Profile.Name)
                .UsingJobData(JobDataKeys.WorkflowName, workflowMatch.Name)
                .UsingJobData(start)
                .WithSimpleSchedule(x => x.WithRepeatCount(0));

        var jobDetail = jobBuilder.Build();
        var trigger = customizesTrigger(triggerBuilder).Build();

        var scheduler = await schedulerFactory.GetScheduler();

        // core: Ensure the job is not already scheduled.
        if (await scheduler.CheckExists(jobDetail.Key))
        {
            throw new WorkflowAlreadyScheduledException();
        }

        logger.LogInformation("Workflow '{WorkflowName}' will be executed once at '{Next}'.", workflowMatch.Name, trigger.GetNextFireTimeUtc());

        return await scheduler.ScheduleJob(jobDetail, trigger);
    }
}