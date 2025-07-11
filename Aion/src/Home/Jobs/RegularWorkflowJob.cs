using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Util.Scriban;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Home.Jobs;

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
        var executionId = Guid.NewGuid().ToString("D");
        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflowName, ExecutionId = executionId });

        if (await IsLocked(workflowPath))
        {
            // note: Logging is done over there.
            return;
        }

        try
        {
            switch (await Workflow.FromFile(workflowPath))
            {
                // core: Gets rid of useless workflows.
                case { Enabled: false }:
                    logger.LogWarning("Unscheduling workflow because it is disabled.");
                    await scheduler.Delete(workflowName);
                    break;
                // core: Gets rid of useless workflows.
                case { Steps: { } steps } when steps.Any(s => s.Enabled) == false:
                    logger.LogWarning("Unscheduling workflow because it has no enabled steps.");
                    await scheduler.Delete(workflowName);
                    break;
                // core: This is where the actual magic happens.
                case var workflow:
                    await process.Start(workflow, ImmutableList<VariableGroup>.Empty.Add(new WorkflowVariableGroup
                    {
                        Name = workflowName,
                        Trigger = nameof(WorkflowTriggerType.Cron),
                        ExecutionId = executionId,
                    }));
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unscheduling workflow because it could not be loaded.");
            await scheduler.Delete(workflowName);
        }
    }

    private async Task<bool> IsLocked(string workflowPath)
    {
        try
        {
            if (await WorkflowLock.FromFile(workflowPath) is { } workflowLock)
            {
                await using (workflowLock)
                {
                    logger.LogWarning("Workflow is locked for {RemainingLock} minutes until it expires on {ExpiresOn}.", workflowLock.Remaining, workflowLock.EndsOnUtc);
                    return workflowLock.IsPending;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking if workflow is locked.");
        }

        return false;
    }
}