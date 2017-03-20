using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using Aion.Data;
using Aion.Data.Configs;
using Aion.Jobs;
using Quartz;
using Quartz.Impl;
using Reusable.Logging;

namespace Aion.Services
{
    public partial class CronService : ServiceBase
    {
        private static readonly ILogger Logger = LoggerFactory.CreateLogger(nameof(CronService));

        private LogEntry _runtimeLogEntry;

        private static CronService _instance;

        private readonly ConcurrentBag<RobotScheme> _robots = new ConcurrentBag<RobotScheme>();

        private CronService()
        {
            InitializeComponent();

            Scheduler = new CronScheduler();

            Scheduler.ScheduleJob<RobotConfigUpdater>(
                name: nameof(RobotConfigUpdater),
                cronExpression: AppSettingsConfig.Jobs.RobotConfigUpdater.Schedule,
                startImmediately: false
            );
        }

        public static CronService Instance => _instance ?? (_instance = new CronService());

        public CronScheduler Scheduler { get; }

        #region ServiceBase members

        protected override void OnStart(string[] args)
        {
            Scheduler.Start();
            LogEntry.New().Info().Message("Service started.").Log(Logger);
            _runtimeLogEntry = LogEntry.New().Stopwatch(sw => sw.Start());
        }

        protected override void OnStop()
        {
            Scheduler.Shutdown();
            _runtimeLogEntry.Message("Service stopped. Elapsed time {ElapsedHours} h.").Log(Logger);
        }

        [Conditional("DEBUG")]
        internal void DebugStart()
        {
            LogEntry.New().Warn().Message("Starting in DEBUG mode.").Log(Logger);
            OnStart(null);
        }

        #endregion

        public static void Start()
        {
            try
            {
                if (Environment.UserInteractive)
                {
                    Instance.DebugStart();
                }
                else
                {
                    Run(Instance);
                }
            }
            catch (Exception ex)
            {
                LogEntry.New().Error().Exception(ex).Message("Error starting service.").Log(Logger);
            }
        }
    }
}