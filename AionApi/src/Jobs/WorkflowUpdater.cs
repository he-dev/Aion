using System.Threading.Tasks;
using AionApi.Services;
using JetBrains.Annotations;
using Quartz;
using Reusable.Wiretap;
using Reusable.Wiretap.Abstractions;

namespace AionApi.Jobs;

[UsedImplicitly]
[DisallowConcurrentExecution]
internal class WorkflowUpdater : IJob
{
    public WorkflowUpdater(ILogger logger, WorkflowStore store, WorkflowScheduler scheduler)
    {
        Logger = logger;
        Store = store;
        Scheduler = scheduler;
    }

    private ILogger Logger { get; }

    private WorkflowStore Store { get; }

    private WorkflowScheduler Scheduler { get; }

    public async Task Execute(IJobExecutionContext context)
    {
        using var status = Logger.Start("UpdateWorkflows", new { name = context.JobDetail.Key.Name });

        // It's not necessary to delete jobs here.
        // The WorkflowRunner & WorkflowScheduler will take care of that if they find the workflow or it's disabled.

        await foreach (var workflow in Store.EnumerateWorkflows())
        {
            await Scheduler.Schedule(workflow);
        }

        status.Completed();
    }
}