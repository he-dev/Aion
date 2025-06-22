using System.Threading.Tasks;
using Aion.Core.Workflows;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Jobs;

[UsedImplicitly]
[DisallowConcurrentExecution]
internal class SynchronizationJob
(
    ILogger<SynchronizationJob> logger,
    WorkflowDirectory directory,
    WorkflowSchedule scheduler
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Synchronizing workflows.");
        await foreach (var workflow in directory)
        {
            await scheduler.Synchronize(workflow);
        }
    }
}