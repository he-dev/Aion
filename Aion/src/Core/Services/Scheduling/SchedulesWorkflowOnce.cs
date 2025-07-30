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
    private JobBuilder CreatesDefaultJobBuilder(string profileName, string workflowName)
    {
        return
            JobBuilder
                .Create<ExecutesWorkflowOnce>()
                .WithIdentity(workflowName, JobGroupName.From<ExecutesWorkflowOnce>(profileName));
    }

    private TriggerBuilder CreatesDefaultTriggerBuilder(string profileName, string workflowPath, string workflowName, WorkflowTriggerType workflowTriggerType)
    {
        return
            TriggerBuilder
                .Create()
                .WithIdentity(workflowName, JobGroupName.From<ExecutesWorkflowOnce>(profileName))
                .UsingJobData(JobDataKeys.ProfileName, profileName)
                .UsingJobData(JobDataKeys.WorkflowName, workflowPath)
                .UsingJobData(workflowTriggerType)
                .WithSimpleSchedule(x => x.WithRepeatCount(0));
    }

    public async Task<DateTimeOffset> Now(string profileName, Workflow workflow)
    {
        var job = CreatesDefaultJobBuilder(profileName, workflow.Name).Build();

        await EnsureWorkflowNotScheduled(job);

        var trigger =
            CreatesDefaultTriggerBuilder(profileName, workflow.Path, workflow.Name, WorkflowTriggerType.StartNow)
                .StartNow()
                .Build();

        var scheduler = await schedulerFactory.GetScheduler();
        return await scheduler.ScheduleJob(job, trigger);
    }

    public async Task<DateTimeOffset> At(string profileName, Workflow workflow, DateTimeOffset startAt)
    {
        var job = CreatesDefaultJobBuilder(profileName, workflow.Name).Build();

        await EnsureWorkflowNotScheduled(job);

        var trigger =
            CreatesDefaultTriggerBuilder(profileName, workflow.Path, workflow.Name, WorkflowTriggerType.StartAt)
                .StartAt(startAt)
                .Build();

        var scheduler = await schedulerFactory.GetScheduler();
        return await scheduler.ScheduleJob(job, trigger);
    }

    public async Task<DateTimeOffset> In(string profileName, Workflow workflow, TimeSpan delay)
    {
        var job = CreatesDefaultJobBuilder(profileName, workflow.Name).Build();

        await EnsureWorkflowNotScheduled(job);

        var trigger =
            CreatesDefaultTriggerBuilder(profileName, workflow.Path, workflow.Name, WorkflowTriggerType.StartIn)
                .StartAt(DateTimeOffset.UtcNow + delay)
                .Build();

        var scheduler = await schedulerFactory.GetScheduler();
        return await scheduler.ScheduleJob(job, trigger);
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