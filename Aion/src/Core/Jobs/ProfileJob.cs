using System;
using System.Threading.Tasks;
using Aion.Core.Services.Profiles;
using Aion.Meta.Logging;
using Aion.Util.Scheduler;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Jobs;

[DisallowConcurrentExecution]
internal class ProfileJob(ILogger<ProfileJob> logger, SynchronizeProfile synchronizeProfile) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var profileName = context.Trigger.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        using var scope = logger.BeginScopeFrom(new
        {
            JobName = context.JobDetail.Key.Name,
            ProfileName = profileName
        });

        logger.LogDebug("Executing '{JobName}'.", context.JobDetail.Key.Name);
        try
        {
            await synchronizeProfile.Invoke(profileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job '{JobName}' failed.", context.JobDetail.Key.Name);
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }
}