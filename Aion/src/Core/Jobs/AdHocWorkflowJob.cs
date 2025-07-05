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
        var path = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Path))!;

        try
        {
            switch (await Workflow.FromFile(path))
            {
                case { Steps: { } steps } when steps.Any(s => s.Enabled) == false:
                    logger.LogWarning("Canceling workflow '{workflow}' because it has no enabled steps.", workflowName);
                    break;
                case var workflow:
                    logger.LogInformation("Executing workflow '{workflow}' on schedule '{cron}'.", workflowName, workflow.Trigger.CronExpressionString);
                    await execution.Start(workflow, new WorkflowVariableGroup
                    {
                        Name = workflowName,
                        Mode = context.Trigger.JobDataMap.GetString("start")! // .. This is always set.
                    });
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Canceling workflow '{workflow}' because it could not be loaded.", path);
        }
    }
}