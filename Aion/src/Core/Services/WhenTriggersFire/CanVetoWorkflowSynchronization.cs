using System.Threading;
using System.Threading.Tasks;
using Aion.Meta.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Core.Services.WhenTriggersFire;

public class CanVetoWorkflowSynchronization
(
    ILogger<CanVetoWorkflowSynchronization> logger,
    IOptions<EngineOptions> engineOptions
) : ITriggerListener
{
    public string Name => nameof(CanVetoWorkflowSynchronization);

    public async Task<bool> VetoJobExecution(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = new())
    {
        if (!engineOptions.Value.SyncOn)
        {
            using var scope = logger.BeginScopeFrom(new { TriggerName = trigger.Key.Name, ProfileName = trigger.JobDataMap.GetString(JobDataKeys.ProfileName) });
            logger.LogWarning("Workflow synchronization is off - pausing it.");
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