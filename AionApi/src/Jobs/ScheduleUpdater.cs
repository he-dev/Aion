using System.Linq;
using System.Threading.Tasks;
using AionApi.Services;
using JetBrains.Annotations;
using Quartz;
using Reusable.Wiretap;
using Reusable.Wiretap.Abstractions;

namespace AionApi.Jobs;

[UsedImplicitly]
[DisallowConcurrentExecution]
internal class ScheduleUpdater : IJob
{
    public ScheduleUpdater(ILogger logger, WorkflowStore store, WorkflowScheduler scheduler)
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
        using var status = Logger.Begin("UpdateWorkflows", details: new { name = context.JobDetail.Key.Name });
        
        await foreach (var workflow in Store)
        {
            await Scheduler.Schedule(workflow);
        }
    }
}