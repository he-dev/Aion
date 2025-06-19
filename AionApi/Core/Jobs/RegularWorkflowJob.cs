using System.Linq;
using System.Threading.Tasks;
using AionApi.Workflows;
using Microsoft.Extensions.Logging;
using Quartz;

namespace AionApi.Jobs;

[DisallowConcurrentExecution]
public class RegularWorkflowJob
(
    ILogger<RegularWorkflowJob> logger,
    WorkflowDirectory directory,
    WorkflowSchedule scheduler,
    WorkflowProcess process
)
    : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var workflowName = context.JobDetail.Key.Name;

        if (await directory.FindWorkflow(workflowName) is { } workflow)
        {
            if (!workflow.Enabled)
            {
                logger.LogWarning("Workflow {workflow} is disabled.", workflowName);
                await scheduler.Delete(workflowName);
                return;
            }

            if (!workflow.Steps.Any(s => s.Enabled))
            {
                logger.LogWarning("Workflow {workflow} has no enabled steps.", workflowName);
                await scheduler.Delete(workflowName);
                return;
            }

            logger.LogInformation("Workflow {workflow} is being executed by the schedule.", workflowName);
            await foreach (var _ in process.Start(workflow)) { }
        }
        else
        {
            logger.LogWarning("Workflow {workflow} not found. Deleting from schedule", workflowName);
            await scheduler.Delete(workflowName);
        }
    }
}