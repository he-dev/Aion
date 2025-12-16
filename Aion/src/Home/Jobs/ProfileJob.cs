using System;
using System.Threading.Tasks;
using Aion.Core.Commands.Workflows;
using Aion.Core.Quartz;
using Aion.Util.Logging;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Home.Jobs;

[DisallowConcurrentExecution]
internal class ProfileJob(ILogger<ProfileJob> logger, SynchronizeProfile synchronizeProfile) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var profileName = context.Trigger.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });
        logger.LogDebug("Executing '{JobName}'.", nameof(ProfileJob));
        try
        {
            await synchronizeProfile.Invoke(profileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job failed.");
        }
    }
}