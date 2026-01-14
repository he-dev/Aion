using System.Threading.Tasks;
using Aion.Modules.Scheduler;
using Aion.Premise.Jobs;
using Aion.Toolbox.Quartz;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Premise.Services.Commands;

public class ScheduleProfile
(
    ILogger<ScheduleProfile> logger,
    IOptions<SchedulerOptions> options,
    ISchedulerFactory schedulerFactory
)
{
    public async Task Invoke()
    {
        var scheduler = await schedulerFactory.GetScheduler();

        foreach (var (profileName, profile) in options.Value.Profiles)
        {
            if (!profile.Sync.Enabled)
            {
                logger.LogWarning("Skipping profile '{ProfileName}' because it is not configured to sync.", profileName);
                continue;
            }

            var jobDetail = JobBuilder
                .Create<ProfileJob>()
                .WithIdentity("sync-profile", new JobGroup<ProfileJob>(profileName))
                .StoreDurably()
                .Build();

            await scheduler.AddJob(jobDetail, true);

            // core: Schedule cron sync.
            await scheduler.ScheduleJob(
                TriggerBuilder
                    .Create()
                    .ForJob(jobDetail)
                    .WithIdentity("sync-profile-cron", new JobGroup<ProfileJob>(profileName))
                    .UsingJobData(JobDataKeys.ProfileName, profileName)
                    .WithCronSchedule(profile.Sync.Cron)
                    .StartNow()
                    .Build()
            );
        }
    }
}