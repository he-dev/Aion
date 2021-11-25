using System.Threading;
using System.Threading.Tasks;
using Aion.Jobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Triggers;

namespace Aion.Services
{
    internal class CronService : BackgroundService
    {
        private readonly ILogger<CronService> _logger;
        private readonly CustomJobFactory _jobFactory;
        private readonly IOptions<WorkflowScheduler.Options> _schedulerOptions;
        private readonly StdSchedulerFactory _schedulerFactory;

        public CronService
        (
            ILogger<CronService> logger,
            CustomJobFactory jobFactory,
            IOptions<WorkflowScheduler.Options> schedulerOptions,
            StdSchedulerFactory schedulerFactory
        )
        {
            _logger = logger;
            _jobFactory = jobFactory;
            _schedulerOptions = schedulerOptions;
            _schedulerFactory = schedulerFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Service started.");

            var scheduler = await _schedulerFactory.GetScheduler(stoppingToken);
            scheduler.JobFactory = _jobFactory;

            await scheduler.Start(stoppingToken);
            await scheduler.ScheduleJob(
                JobBuilder.Create<WorkflowScheduler>().Build(),
                //new CronTriggerImpl(nameof(WorkflowScheduler), "main", _schedulerOptions.Value.Schedule),
                TriggerBuilder.Create().WithIdentity(nameof(WorkflowScheduler)).WithCronSchedule(_schedulerOptions.Value.Schedule).Build(),
                stoppingToken
            );
        }
    }
}