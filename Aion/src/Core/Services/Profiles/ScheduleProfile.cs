using System.Threading.Tasks;
using Aion.Core.Jobs;
using Aion.Util;
using Aion.Util.Scheduler;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Services.Profiles;

public class ScheduleProfile
(
    ILogger<ScheduleProfile> logger,
    ISchedulerFactory schedulerFactory
)
{
    public async Task For(Profile profile)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        if (!profile.Sync.Enabled)
        {
            logger.LogWarning("Skipping profile '{ProfileName}' because it is disabled.", profile.Name);
            return;
        }

        var jobDetail = JobBuilder
            .Create<ProfileJob>()
            .WithIdentity("sync-profile", profile.Name)
            .StoreDurably()
            .Build();

        await scheduler.AddJob(jobDetail, true);

        // core: Schedule cron sync.
        var next = await scheduler.ScheduleJob(
            TriggerBuilder
                .Create()
                .ForJob(jobDetail)
                .WithIdentity("sync-profile-cron", profile.Name)
                .UsingJobData(JobDataKeys.ProfileName, profile.Name)
                .WithCronSchedule(profile.Sync.Cron)
                .StartNow()
                .Build()
        );

        logger.LogInformation("Profile '{ProfileName}' scheduled. Next synchronization at '{Next}'.", profile.Name, next);
    }
}