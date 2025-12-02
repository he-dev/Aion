using System.Threading.Tasks;
using Aion.Core.Commands.Workflows;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Quartz.Jobs;

[DisallowConcurrentExecution]
internal class SynchronizationJob(ILogger<SynchronizationJob> logger, SynchronizeWorkflows synchronizeWorkflows) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug("Executing workflow synchronization job...");
        var profileName = context.Trigger.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        await synchronizeWorkflows.Invoke(profileName);
    }
}