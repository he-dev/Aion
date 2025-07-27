using System;
using System.Threading.Tasks;
using Aion.Home.Jobs;
using Aion.Util.Quartz;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Flairs.Scheduling;

// https://www.quartz-scheduler.net/documentation/quartz-3.x/quick-start.html

public class SchedulesWorkflowOnce
(
    ILogger<SchedulesWorkflowOnce> logger,
    ISchedulerFactory schedulerFactory
)
{
    public async Task<DateTimeOffset> Now(Workflow workflow)
    {
        var job =
            JobBuilder
                .Create<ExecutesWorkflowOnce>()
                .WithIdentity(workflow.Name, new GroupName<ExecutesWorkflowOnce>(""))
                .UsingJobData(nameof(Workflow.Path), workflow.Path)
                .Build();

        await EnsureWorkflowNotScheduled(job);

        var trigger =
            TriggerBuilder
                .Create()
                .WithIdentity(workflow.Name, new GroupName<ExecutesWorkflowOnce>())
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
                .Create<ExecutesWorkflowOnce>()
                .WithIdentity(workflow.Name, new GroupName<ExecutesWorkflowOnce>())
                .UsingJobData(nameof(Workflow.Path), workflow.Path)
                .Build();

        await EnsureWorkflowNotScheduled(job);

        var trigger =
            TriggerBuilder
                .Create()
                .WithIdentity(workflow.Name, new GroupName<ExecutesWorkflowOnce>())
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
                .Create<ExecutesWorkflowOnce>()
                .WithIdentity(workflow.Name, new GroupName<ExecutesWorkflowOnce>())
                .UsingJobData(nameof(Workflow.Path), workflow.Path)
                .Build();

        await EnsureWorkflowNotScheduled(job);

        var trigger =
            TriggerBuilder
                .Create()
                .WithIdentity(workflow.Name, new GroupName<ExecutesWorkflowOnce>())
                .StartAt(DateTimeOffset.UtcNow + delay)
                .WithSimpleSchedule(x => x.WithRepeatCount(0))
                .UsingJobData(WorkflowTriggerGroup.StartIn)
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
