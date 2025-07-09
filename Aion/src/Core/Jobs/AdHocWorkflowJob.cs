using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Jobs;

public class AdHocWorkflowJob
(
    ILogger<RegularWorkflowJob> logger,
    WorkflowExecution execution
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var workflowName = context.JobDetail.Key.Name;
        var workflowPath = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Path))!;

        var correlationId = Guid.NewGuid().ToString("N");
        using var scope = logger.BeginScope(new { workflow = workflowName, correlationId });

        try
        {
            switch (await Workflow.FromFile(workflowPath))
            {
                case { Steps: { } steps } when steps.Any(s => s.Enabled) == false:
                    logger.LogWarning("Canceling workflow '{workflow}' because it has no enabled steps.", workflowName);
                    break;
                case var workflow:
                    logger.LogInformation("Executing workflow '{workflow}' on schedule '{cron}'.", workflowName, workflow.Trigger.CronExpressionString);
                    await execution.Start(workflow, new WorkflowVariableGroup
                    {
                        Name = workflowName,
                        Mode = context.Trigger.JobDataMap.GetString("start")!, // .. This is always set.
                        JobId = correlationId,
                    });
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Canceling workflow because it could not be loaded.");
        }
    }
}