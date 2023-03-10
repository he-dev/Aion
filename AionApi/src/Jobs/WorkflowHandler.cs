using System.Threading.Tasks;
using AionApi.Services;
using Quartz;
using Reusable.Wiretap;
using Reusable.Wiretap.Abstractions;

namespace AionApi.Jobs;

[DisallowConcurrentExecution]
public class WorkflowHandler : IJob
{
    public WorkflowHandler(ILogger logger, WorkflowStore store, WorkflowScheduler scheduler, WorkflowRunner runner)
    {
        Logger = logger;
        Store = store;
        Scheduler = scheduler;
        Runner = runner;
    }

    private ILogger Logger { get; }

    private WorkflowStore Store { get; }

    private WorkflowScheduler Scheduler { get; }

    private WorkflowRunner Runner { get; }

    public async Task Execute(IJobExecutionContext context)
    {
        var workflowName = context.JobDetail.Key.Name;
        using var status = Logger.Start("HandleWorkflow", new { name = workflowName });

        if (await Store.Get(workflowName) is { } workflow)
        {
            await Runner.Run(workflow);
            status.Completed();
        }
        // The workflow-file might have been deleted.
        else
        {
            await Scheduler.Delete(workflowName);
            Store.Delete(workflowName);
            status.Canceled(new { reason = "Workflow not found.", });
        }
    }
}