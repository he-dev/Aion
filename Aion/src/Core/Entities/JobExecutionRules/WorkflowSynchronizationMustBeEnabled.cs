using System.Threading;
using System.Threading.Tasks;
using Aion.Util.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Core.Entities.JobExecutionRules;

public class WorkflowSynchronizationMustBeEnabled
(
    ILogger<WorkflowSynchronizationMustBeEnabled> logger,
    IOptions<InstanceOptions> engineOptions
) : ITriggerListener
{
    public string Name => nameof(WorkflowSynchronizationMustBeEnabled);

    public async Task<bool> VetoJobExecution(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = new())
    {
        var profileName = trigger.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        var profile = engineOptions.Value[profileName];
        if (!profile.SyncOn)
        {
            using var scope = logger.BeginScopeFrom(new { TriggerName = trigger.Key.Name, ProfileName = trigger.JobDataMap.GetString(JobDataKeys.ProfileName) });
            logger.LogWarning("Profile synchronization for '{ProfileName}' is off - pausing it.", profileName);
            await context.Scheduler.PauseJob(context.JobDetail.Key, cancellationToken);
            return true;
        }

        return false;
    }

    public Task TriggerFired(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = new())
    {
        logger.LogDebug("Workflow synchronization trigger '{TriggerName}' has fired.", trigger.Key.Name);
        return Task.CompletedTask;
    }

    public Task TriggerMisfired(ITrigger trigger, CancellationToken cancellationToken = new())
    {
        logger.LogWarning("Workflow synchronization trigger '{TriggerName}' has misfired.", trigger.Key.Name);
        return Task.CompletedTask;
    }

    public Task TriggerComplete(ITrigger trigger, IJobExecutionContext context, SchedulerInstruction triggerInstructionCode, CancellationToken cancellationToken = new())
    {
        return Task.CompletedTask;
    }
}