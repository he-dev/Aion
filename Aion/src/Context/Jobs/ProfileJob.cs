using System;
using System.Threading.Tasks;
using Aion.Context.Services.Commands;
using Aion.Modules.Scheduler;
using Aion.Toolbox.Logging;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Context.Jobs;

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