using System;
using System.Linq;
using System.Threading.Tasks;
using AionApi.Services;
using Quartz;
using Quartz.Impl.Triggers;
using Reusable.Wiretap;
using Reusable.Wiretap.Abstractions;

namespace AionApi.Jobs;

[DisallowConcurrentExecution]
public class WorkflowLauncher : IJob
{
    public WorkflowLauncher(ILogger logger, WorkflowStore store, WorkflowScheduler scheduler, WorkflowProcess process)
    {
        Logger = logger;
        Store = store;
        Scheduler = scheduler;
        Process = process;
    }

    private ILogger Logger { get; }

    private WorkflowStore Store { get; }

    private WorkflowScheduler Scheduler { get; }

    private WorkflowProcess Process { get; }

    public async Task Execute(IJobExecutionContext context)
    {
        var workflowName = context.JobDetail.Key.Name;
        using var status = Logger.Begin("ExecuteSchedule", details: new
        {
            workflow = new { name = workflowName, schedule = ((ICronTrigger)context.Trigger).CronExpressionString },
            localNow = DateTime.Now
        });

        if (await Store.GetWorkflow(workflowName) is { Enabled: true, IsEmpty: false } workflow)
        {
            await foreach (var _ in Process.Start(workflow)) { }
            status.LogEnd();
        }
        else
        {
            await Scheduler.Delete(workflowName);
            status.LogStop(message: "Workflow not found or disabled.");
        }
    }
}