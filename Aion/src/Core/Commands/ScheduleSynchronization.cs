using System.Threading.Tasks;
using Aion.Core.Options;
using Aion.Core.Quartz;
using Aion.Core.Quartz.Jobs;
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
            if (!profile.Enabled)
            {
                logger.LogWarning("Skipping profile '{ProfileName}' because it is not configured to sync.", profileName);
                continue;
            }

            var jobDetail = JobBuilder
                .Create<SynchronizationJob>()
                .WithIdentity("sync-workflows", GroupName.For<SynchronizationJob>(profileName))
                .Build();

            await scheduler.AddJob(jobDetail, true);

            // core: Schedule cron sync.
            await scheduler.ScheduleJob(
                TriggerBuilder.Create()
                    .ForJob(jobDetail)
                    .WithIdentity("sync-workflows-cron", GroupName.For<SynchronizationJob>(profileName))
                    .UsingJobData(JobDataKeys.ProfileName, profileName)
                    .WithCronSchedule(profile.Sync)
                    .Build()
            );

            // core: Schedule once sync.
            await scheduler.ScheduleJob(
                TriggerBuilder.Create()
                    .ForJob(jobDetail)
                    .WithIdentity("sync-workflows-once", GroupName.For<SynchronizationJob>(profileName))
                    .UsingJobData(JobDataKeys.ProfileName, profileName)
                    .WithSimpleSchedule(x => x.WithRepeatCount(0))
                    .StartNow()
                    .Build()
            );
        }
    }
}