using System.Threading.Tasks;
using Aion.Core.Options;
using Aion.Core.Quartz;
using Aion.Home.Jobs;
using Aion.Util.Quartz;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Core.Commands;

public class ScheduleSynchronization
(
    ILogger<ScheduleSynchronization> logger,
    IOptions<SchedulerOptions> options,
    ISchedulerFactory schedulerFactory
)
{
    public async Task Execute()
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
                .WithIdentity("sync-profile", GroupName.For<ProfileJob>(profileName))
                .StoreDurably()
                .Build();

            await scheduler.AddJob(jobDetail, true);

            // core: Schedule cron sync.
            await scheduler.ScheduleJob(
                TriggerBuilder.Create()
                    .ForJob(jobDetail)
                    .WithIdentity("sync-profile-cron", GroupName.For<ProfileJob>(profileName))
                    .UsingJobData(JobDataKeys.ProfileName, profileName)
                    .WithCronSchedule(profile.Sync.Cron)
                    .Build()
            );

            // core: Schedule once sync.
            await scheduler.ScheduleJob(
                TriggerBuilder.Create()
                    .ForJob(jobDetail)
                    .WithIdentity("sync-profile-once", GroupName.For<ProfileJob>(profileName))
                    .UsingJobData(JobDataKeys.ProfileName, profileName)
                    .WithSimpleSchedule(x => x.WithRepeatCount(0))
                    .StartNow()
                    .Build()
            );
        }
    }
}