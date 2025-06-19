using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AionApi.Utilities;
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
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var workflowName = context.JobDetail.Key.Name;

        logger.LogInformation("Executing workflow '{workflow}' on schedule.", workflowName);

        if (await directory.FindWorkflow(workflowName) is { } workflow)
        {
            if (!workflow.Enabled)
            {
                logger.LogWarning("Canceling workflow '{workflow}' because it is disabled.", workflowName);
                await scheduler.Delete(workflowName);
                return;
            }

            if (!workflow.Steps.Any(s => s.Enabled))
            {
                logger.LogWarning("Canceling workflow '{workflow}' because it has no enabled steps.", workflowName);
                await scheduler.Delete(workflowName);
                return;
            }

            var jobVariables = new VariableGroup("job")
            {
                ["name"] = workflowName,
                ["start"] = "cron",
                ["cron"] = ((ICronTrigger)context.Trigger).CronExpressionString,
            };

            await foreach (var _ in process.Start(workflow, jobVariables)) { }
        }
        else
        {
            logger.LogWarning("Deleting workflow '{workflow}' from schedule because it was not found.", workflowName);
            await scheduler.Delete(workflowName);
        }
    }
}