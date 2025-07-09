using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Jobs;

[DisallowConcurrentExecution]
public class RegularWorkflowJob
(
    ILogger<RegularWorkflowJob> logger,
    WorkflowSchedule scheduler,
    WorkflowExecution execution
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var workflowName = context.JobDetail.Key.Name;
        var workflowPath = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Path))!;

        if (await IsLocked(workflowName, workflowPath))
        {
            return;
        }

        var jobId = Guid.NewGuid().ToString("N");
        using var scope = logger.BeginScope(new { workflow = workflowName, jobId });

        try
        {
            switch (await Workflow.FromFile(workflowPath))
            {
                case { Enabled: false }:
                    logger.LogWarning("Unscheduling workflow because it is disabled.");
                    await scheduler.Delete(workflowName);
                    break;
                case { Steps: { } steps } when steps.Any(s => s.Enabled) == false:
                    logger.LogWarning("Unscheduling workflow '{workflow}' because it has no enabled steps.", workflowName);
                    await scheduler.Delete(workflowName);
                    break;
                case var workflow:
                    logger.LogInformation("Executing workflow '{workflow}' on schedule '{cron}'.", workflowName, workflow.Trigger.CronExpressionString);
                    await execution.Start(workflow, new WorkflowVariableGroup
                    {
                        Name = workflowName,
                        Mode = "cron",
                        Cron = ((ICronTrigger)context.Trigger).CronExpressionString,
                        JobId = jobId,
                    });
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unscheduling workflow because it could not be loaded.");
            await scheduler.Delete(workflowName);
        }
    }

    private async Task<bool> IsLocked(string workflowName, string workflowPath)
    {
        try
        {
            if (await WorkflowLock.FromFile(workflowPath) is { } workflowLock)
            {
                await using (workflowLock)
                {
                    logger.LogWarning
                    (
                        "Workflow '{workflow}' is locked for {remaining} minutes until {expiresOnUtc}.",
                        workflowName, workflowLock.Remaining, workflowLock.EndsOnUtc
                    );
                    return workflowLock.IsPending;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if workflow '{workflow}' is locked.", workflowPath);
        }

        return false;
    }
}