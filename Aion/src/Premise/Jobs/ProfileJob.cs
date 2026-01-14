using System;
using System.Threading.Tasks;
using Aion.Modules.Scheduler;
using Aion.Premise.Services.Commands;
using Aion.Toolbox.Logging;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Premise.Jobs;

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