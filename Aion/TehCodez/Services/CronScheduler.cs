using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Aion.Data;
using Aion.Extensions;
using Aion.Jobs;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Triggers;
using Reusable.Logging;

namespace Aion.Services
{
    public class CronScheduler
    {
        private readonly ConcurrentDictionary<string, ProcessGroup> _robots = new ConcurrentDictionary<string, ProcessGroup>();

        private readonly IScheduler _quartzScheduler;

        private static readonly ILogger Logger;

        static CronScheduler()
        {
            Logger = LoggerFactory.CreateLogger(nameof(CronScheduler));
        }

        public CronScheduler()
        {
            var schedulerFactory = new StdSchedulerFactory();
            _quartzScheduler = schedulerFactory.GetScheduler();
            _quartzScheduler.Clear();
        }

        public IEnumerable<string> GetJobSchedules(string jobName)
        {
            return
                _quartzScheduler
                    .GetTriggersOfJob(new JobKey(jobName))
                    .OfType<CronTriggerImpl>()
                    .Select(x => x.CronExpressionString);
        }

        public bool TryGetProcessGroup(string name, out ProcessGroup scheme) => _robots.TryGetValue(name, out scheme);

        public void Start() => _quartzScheduler.Start();

        public void Shutdown() => _quartzScheduler.Shutdown(true);

        public void ScheduleRobots(params ProcessGroup[] robotSchemes)
        {
            foreach (var scheme in robotSchemes)
            {
                try
                {
                    ScheduleRobots(scheme);
                }
                catch (Exception ex)
                {
                    LogEntry.New().Error().Exception(ex).Message($"Error scheduling {scheme.FileName.DoubleQuote()}.").Log(Logger);
                }
            }
        }

        public void ScheduleRobots(ProcessGroup scheme)
        {
            if (scheme.Enabled)
            {
                if (_robots.TryGetValue(scheme, out ProcessGroup currentScheme))
                {
                    RescheduleJob(scheme, scheme.Schedule.Trim(), scheme.StartImmediately);
                    _robots.TryUpdate(scheme, scheme, currentScheme);
                }
                else
                {
                    ScheduleJob<RobotLaucher>(scheme, scheme.Schedule.Trim(), scheme.StartImmediately);
                    _robots.TryAdd(scheme, scheme);
                }
            }
            else
            {
                UnscheduleJob(scheme);
                _robots.TryRemove(scheme, out ProcessGroup removedScheme);
            }
        }

        public void ScheduleJob<TJob>(string name, string cronExpression, bool startImmediately) where TJob : IJob
        {
            var jobDetail = JobBuilder.Create<TJob>().WithIdentity(new JobKey(name)).Build();
            var trigger = TriggerFactory.CreateTrigger(name, cronExpression, startImmediately);
            _quartzScheduler.ScheduleJob(jobDetail, trigger);
            LogEntry.New().Info().Message($"Job {name.DoubleQuote()} scheduled to {cronExpression.DoubleQuote()}.").Log(Logger);
        }

        public bool RescheduleJob(string name, string cronExpression, bool startImmediately)
        {
            var schedules = GetJobSchedules(name).ToList();

            if (!schedules.Any())
            {
                LogEntry.New().Warn().Message($"Job {name.DoubleQuote()} isn't scheduled.").Log(Logger);
                return false;
            }

            // Multiple schedules are not supported.
            var schedule0 = schedules.First();

            if (schedule0 == cronExpression)
            {
                LogEntry.New().Debug().Message($"Job {name.DoubleQuote()} doesn't need to be rescheduled.").Log(Logger);
                return false;
            }
            else
            {
                var trigger = TriggerFactory.CreateTrigger(name, cronExpression, startImmediately);
                _quartzScheduler.RescheduleJob(trigger.Key, trigger);
                LogEntry.New().Info().Message($"Job {name.DoubleQuote()} schedule changed from {schedule0.DoubleQuote()} to {cronExpression.DoubleQuote()}").Log(Logger);
                return true;
            }
        }

        public void UnscheduleJob(string name)
        {
            _quartzScheduler.UnscheduleJob(new TriggerKey(name));
            LogEntry.New().Info().Message($"Job {name.DoubleQuote()} unscheduled.").Log(Logger);
        }
    }
}
