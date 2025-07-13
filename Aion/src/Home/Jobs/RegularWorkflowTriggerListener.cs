using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Home.Jobs;

public class RegularWorkflowTriggerListener
(
    ILogger<RegularWorkflowTriggerListener> logger
) : ITriggerListener
{
    public string Name => nameof(RegularWorkflowTriggerListener);

    public async Task<bool> VetoJobExecution(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var workflowPath = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Path))!;

        try
        {
            if (await WorkflowLock.FromFile(workflowPath) is { } workflowLock)
            {
                if (workflowLock.IsExpired)
                {
                    logger.LogWarning("Workflow lock has expired on {ExpiresOn} and will be deleted.", workflowLock.EndsOnUtc.ToLocalTime());
                    await workflowLock.Delete();
                }

                return workflowLock.IsRunning;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow lock could not be checked.");
            // core: Use the fail-close principle. If not sure whether it's open, then it's closed.
            return true;
        }

        return false;
    }

    public Task TriggerFired(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task TriggerMisfired(ITrigger trigger, CancellationToken cancellationToken = default)
    {
        logger.LogWarning("Regular workflow job trigger '{TriggerName}' has misfired.", trigger.Key.Name);
        return Task.CompletedTask;
    }

    public Task TriggerComplete(ITrigger trigger, IJobExecutionContext context, SchedulerInstruction triggerInstructionCode, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}