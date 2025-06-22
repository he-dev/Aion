using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Utilities;
using Aion.Workflows;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Jobs;

[DisallowConcurrentExecution]
public class RegularWorkflowJob
(
    ILogger<RegularWorkflowJob> logger,
    WorkflowDirectory directory,
    WorkflowSchedule scheduler,
    WorkflowProcess process,
    MaintenanceToken maintenanceToken
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var workflowName = context.JobDetail.Key.Name;

        var pendingTokens = await maintenanceToken.Pending();
        if (pendingTokens.FirstOrDefault(t => t.Matches(workflowName)) is { } token)
        {
            logger.LogWarning("Canceling workflow '{workflow}' because maintenance token '{token}' is pending. Remaining time: {remaining}", workflowName, token.Filter, token.Remaining);
            return;
        }

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
                ["mode"] = "cron",
                ["cron"] = ((ICronTrigger)context.Trigger).CronExpressionString,
            };

            logger.LogInformation("Executing workflow '{workflow}' on schedule.", workflowName);
            await foreach (var _ in process.Start(workflow, jobVariables)) { }
        }
        else
        {
            logger.LogWarning("Deleting workflow '{workflow}' from schedule because it was not found.", workflowName);
            await scheduler.Delete(workflowName);
        }
    }
}