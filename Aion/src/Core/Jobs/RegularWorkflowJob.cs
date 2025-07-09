using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Jobs;

[DisallowConcurrentExecution]
public class RegularWorkflowJob
(
    ILogger<RegularWorkflowJob> logger,
    WorkflowSchedule scheduler,
    WorkflowProcess process
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

        var executionId = Guid.NewGuid().ToString("D");
        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflowName, ExecutionId = executionId });

        try
        {
            switch (await Workflow.FromFile(workflowPath))
            {
                case { Enabled: false }:
                    logger.LogWarning("Unscheduling workflow because it is disabled.");
                    await scheduler.Delete(workflowName);
                    break;
                case { Steps: { } steps } when steps.Any(s => s.Enabled) == false:
                    logger.LogWarning("Unscheduling workflow because it has no enabled steps.");
                    await scheduler.Delete(workflowName);
                    break;
                case var workflow:
                    logger.LogInformation("Executing workflow on schedule by '{Cron}'.", workflow.Trigger.CronExpressionString);
                    await process.Start(workflow, new WorkflowVariableGroup
                    {
                        Name = workflowName,
                        Mode = nameof(WorkflowTriggerType.Cron),
                        Trigger = ((ICronTrigger)context.Trigger).CronExpressionString,
                        JobId = executionId,
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