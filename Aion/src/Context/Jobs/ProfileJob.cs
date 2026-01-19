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