using System.Threading.Tasks;
using Aion.Context.Jobs;
using Aion.Modules.Scheduler;
using Aion.Modules.Services.Queries;
using Aion.Toolbox.Quartz;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Context.Services.Commands;

public class ScheduleProfile
(
    ILogger<ScheduleProfile> logger,
    GetProfile getProfile,
    ISchedulerFactory schedulerFactory
)
{
    public async Task Invoke()
    {
        var scheduler = await schedulerFactory.GetScheduler();

        foreach (var profile in getProfile.All())
        {
            if (!profile.Sync.Enabled)
            {
                logger.LogWarning("Skipping profile '{ProfileName}' because it is not configured to sync.", profile.Name);
                continue;
            }

            var jobDetail = JobBuilder
                .Create<ProfileJob>()
                .WithIdentity("sync-profile", profile.Name)
                .StoreDurably()
                .Build();

            await scheduler.AddJob(jobDetail, true);

            // core: Schedule cron sync.
            await scheduler.ScheduleJob(
                TriggerBuilder
                    .Create()
                    .ForJob(jobDetail)
                    .WithIdentity("sync-profile-cron", profile.Name)
                    .UsingJobData(JobDataKeys.ProfileName, profile.Name)
                    .WithCronSchedule(profile.Sync.Cron)
                    .StartNow()
                    .Build()
            );
        }
    }
}