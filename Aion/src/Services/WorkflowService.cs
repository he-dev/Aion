using System.Threading;
using System.Threading.Tasks;
using Aion.Jobs;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Spi;

namespace Aion.Services
{
    internal class WorkflowService : BackgroundService
    {
        private readonly ILogger<WorkflowService> _logger;
        private readonly IJobFactory _jobFactory;
        private readonly IOptions<WorkflowService.Options> _options;
        private readonly ISchedulerFactory _schedulerFactory;

        public WorkflowService
        (
            ILogger<WorkflowService> logger,
            IJobFactory jobFactory,
            IOptions<WorkflowService.Options> options,
            ISchedulerFactory schedulerFactory
        )
        {
            _logger = logger;
            _jobFactory = jobFactory;
            _options = options;
            _schedulerFactory = schedulerFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Service started.");

            var scheduler = await _schedulerFactory.GetScheduler(stoppingToken);
            scheduler.JobFactory = _jobFactory;

            await scheduler.Start(stoppingToken);
            await scheduler.ScheduleJob
            (
                JobBuilder.Create<WorkflowScheduler>().Build(),
                TriggerBuilder.Create().WithIdentity(nameof(WorkflowScheduler)).WithCronSchedule(_options.Value.Schedule).Build(),
                stoppingToken
            );
        }
        
        [UsedImplicitly]
        internal class Options
        {
            public string Schedule { get; set; } = null!;
            public string WorkflowsDirectory { get; set; } = null!;
        }
    }
}