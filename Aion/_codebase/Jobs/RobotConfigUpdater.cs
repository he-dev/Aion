using System;
using System.Linq;
using Aion.Data.Configs;
using Aion.Services;
using Quartz;
using Reusable.Logging;

namespace Aion.Jobs
{
    [DisallowConcurrentExecution]
    class RobotConfigUpdater : IJob
    {
        private static readonly ILogger Logger;

        static RobotConfigUpdater()
        {
            Logger = LoggerFactory.CreateLogger(nameof(RobotConfigUpdater));
        }

        public void Execute(IJobExecutionContext context)
        {
            try
            {
                var schemes = SchemeReader.ReadSchemes(AppSettingsConfig.Paths.RobotsDirectoryName).ToArray();
                CronService.Instance.Scheduler.ScheduleRobots(schemes);
            }
            catch (Exception ex)
            {
                LogEntry.New().Error().Exception(ex).Message("Error scheduling robots.").Log(Logger);
            }
        }
    }
}
