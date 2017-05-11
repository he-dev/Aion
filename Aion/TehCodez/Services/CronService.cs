using System.ServiceProcess;
using Reusable.Logging;

namespace Aion.Services
{
    public partial class CronService : ServiceBase, IService
    {
        private static readonly ILogger Logger = LoggerFactory.CreateLogger(nameof(CronService));

        private LogEntry _runtimeLogEntry;

        public CronService()
        {
            InitializeComponent();
            Scheduler = new CronScheduler();
        }

        public CronScheduler Scheduler { get; }

        #region ServiceBase

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

        #endregion

        void IService.Start(params string[] args)
        {
            OnStart(args);
        }
    }
}