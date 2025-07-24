using System.Threading;
using System.Threading.Tasks;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Core.Listeners;

public class CanVetoProfileSynchronization
(
    ILogger<CanVetoProfileSynchronization> logger,
    IOptions<EngineOptions> engineOptions
) : ITriggerListener
{
    public string Name => nameof(CanVetoProfileSynchronization);

    public Task TriggerFired(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = new())
    {
        return Task.CompletedTask;
    }

    public async Task<bool> VetoJobExecution(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = new())
    {
        if (!engineOptions.Value.SyncOn)
        {
            using var scope = logger.BeginScopeFrom(new { TriggerName = trigger.Key.Name, ProfileName = trigger.JobDataMap.GetString(JobDataKeys.ProfileName) });
            logger.LogWarning("Profile synchronization is off - pausing it.");
            await context.Scheduler.PauseJob(context.JobDetail.Key, cancellationToken);
            return true;
        }

        return false;
    }

    public Task TriggerMisfired(ITrigger trigger, CancellationToken cancellationToken = new())
    {
        return Task.CompletedTask;
    }

    public Task TriggerComplete(ITrigger trigger, IJobExecutionContext context, SchedulerInstruction triggerInstructionCode, CancellationToken cancellationToken = new())
    {
        return Task.CompletedTask;
    }
}