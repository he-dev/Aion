using System;
using Aion.Data;
using Aion.Data.Configuration;
using Aion.Services;
using Reusable.Logging;
using Aion.Jobs;
using Reusable.ConfigWhiz;
using Reusable.ConfigWhiz.Datastores.AppConfig;

namespace Aion
{
    internal class Program
    {
        public const string InstanceName = "Aion";
        public const string InstanceVersion = "4.0.0";

        private static readonly ILogger Logger;

        public static Configuration Configuration { get; }

        static Program()
        {
            InitializeLogging();
            Logger = LoggerFactory.CreateLogger(nameof(Program));
            Configuration = InitializeConfiguraiton();
        }

        private static void Main()
        {
            //Logger.Info().MessageFormat("*** {Name} v{Version} ***", new { Name = InstanceName, Version = InstanceVersion }).Log();

            try
            {
                var cronService = ServiceStarter.Start<CronService>();
                RobotJob.Scheduler = cronService.Scheduler;

                cronService.Scheduler.ScheduleJob<RobotConfigUpdater>(
                    name: nameof(RobotConfigUpdater),
                    cronExpression: Configuration.Load<Program, Global>().RobotConfigUpdaterSchedule,
                    startImmediately: false
                );
            }
            catch (Exception ex)
            {
                LogEntry.New().Error().Exception(ex).Message("Error starting service.").Log(Logger);
            }

            if (Environment.UserInteractive)
            {
                Console.ReadKey();
            }
        }

        private static void InitializeLogging()
        {
            Reusable.Logging.NLog.Tools.LayoutRenderers.InvariantPropertiesLayoutRenderer.Register();
            Reusable.Logging.NLog.Tools.DatabaseTargetQueryGenerator.GenerateInsertQueries();

            Reusable.Logging.Logger.ComputedProperties.Add(new Reusable.Logging.ComputedProperties.AppSetting(name: "Environment", key: $"{InstanceName}.Environment"));
            Reusable.Logging.Logger.ComputedProperties.Add(new Reusable.Logging.ComputedProperties.ElapsedSeconds());
            Reusable.Logging.Logger.ComputedProperties.Add(new Reusable.Logging.ComputedProperties.ElapsedHours());

            Reusable.Logging.LoggerFactory.Initialize<Reusable.Logging.Adapters.NLogFactory>();
        }

        private static Configuration InitializeConfiguraiton()
        {
            //var loadSettingsLogger = Logger.Info().Message("Config loaded.").StartStopwatch();
            try
            {
                var configuration = new Reusable.ConfigWhiz.Configuration(new AppSettings());
                configuration.Load<Program, Data.Configuration.Global>();
                return configuration;
            }
            catch (Exception ex)
            {
                //loadSettingsLogger.Error().Exception(ex);
                throw;
            }
            finally
            {
                //loadSettingsLogger.Log();
            }

        }
    }

    internal abstract class RobotJob
    {
        // This is a workaround for the job-data-map because it really sucks.
        public static CronScheduler Scheduler { get; set; }
    }
}
