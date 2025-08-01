using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Core.Services.WhenTriggersFire;

public class CanVetoWorkflowExecution
(
    ILogger<CanVetoWorkflowExecution> logger,
    IOptions<EngineOptions> engineOptions
) : ITriggerListener
{
    public string Name => nameof(CanVetoWorkflowExecution);

    public async Task<bool> VetoJobExecution(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var profileName = trigger.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        var workflowName = trigger.JobDataMap.GetString(JobDataKeys.WorkflowName)!;

        var profile = engineOptions.Value[profileName];
        try
        {
            var workflowMatch = profile.Workflow(workflowName);
            if (await MaintenancePeriod.FromFile(workflowMatch.Path) is { } workflowLock)
            {
                if (workflowLock.Status == MaintenancePeriodStatus.Expired)
                {
                    logger.LogWarning("Workflow lock has expired on {ExpiresOn} and will be deleted.", workflowLock.EndsOnUtc.ToLocalTime());
                    await workflowLock.Cancel();
                }

                return workflowLock.Status == MaintenancePeriodStatus.Running;
            }
        }
        catch (FileNotFoundException)
        {
            // core: Ignore this error as it's by design.
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