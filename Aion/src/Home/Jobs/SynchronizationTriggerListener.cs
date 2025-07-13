using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Home.Jobs;

public class SynchronizationTriggerListener
(
    ILogger<SynchronizationTriggerListener> logger,
    IOptions<SynchronizationJobOptions> options
) : ITriggerListener
{
    public string Name => nameof(SynchronizationTriggerListener);

    public Task TriggerFired(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = new())
    {
        return Task.CompletedTask;
    }

    public async Task<bool> VetoJobExecution(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken = new())
    {
        if (options.Value.IsOn == false)
        {
            logger.LogWarning("Synchronization job is disabled - pausing it.");
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