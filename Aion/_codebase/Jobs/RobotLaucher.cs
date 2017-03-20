using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Aion.Data;
using Aion.Data.Configs;
using Aion.Extensions;
using Aion.Services;
using Quartz;
using Reusable.Logging;

namespace Aion.Jobs
{
    [DisallowConcurrentExecution]
    class RobotLaucher : IJob
    {
        private static readonly RobotFileNameResolver RobotFileNameResolver = new RobotFileNameResolver(Directory.GetDirectories);
        private static readonly ILogger Logger = LoggerFactory.CreateLogger(nameof(RobotLaucher));

        public void Execute(IJobExecutionContext context)
        {
            var schemeName = context.JobDetail.Key.Name;
            if (CronService.Instance.Scheduler.TryGetRobotScheme(schemeName, out RobotScheme scheme))
            {
                LaunchRobots(scheme);
            }
        }

        private static void LaunchRobots(RobotScheme scheme)
        {
            foreach (var robotConfig in scheme.Robots.Where(r => r.Enabled))
            {
                try
                {
                    if (!LaunchRobot(AppSettingsConfig.Paths.RobotsDirectoryName, robotConfig)) break;
                }
                catch (Exception ex)
                {
                    LogEntry.New().Error().Exception(ex).Message($"Error starting '{robotConfig.FileName}'.").Log(Logger);
                    break;
                }
            }
        }

        private static bool LaunchRobot(string robotsDirectoryName, RobotInfo robot)
        {
            var robotFileName = RobotFileNameResolver.Resolve(robotsDirectoryName, robot.FileName);
            if (string.IsNullOrEmpty(robotFileName))
            {
                throw new FileNotFoundException($"File not found '{robotFileName}'.");
            }

            if (IsRunning(robot.FileName, robot.Arguments))
            {
                throw new InvalidOperationException($"Process already running '{robotFileName}'.");
            }

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = robotFileName,
                Arguments = robot.Arguments,
                WindowStyle = robot.WindowStyle,
                //UseShellExecute = false,
            });

            if (process == null)
            {
                throw new Exception($"Error starting {robotFileName.DoubleQuote()}.");
            }

            LogEntry.New().Info().Message($"Started {robotFileName.DoubleQuote()}").Log(Logger);

            using (var logEntry = LogEntry.New().Info().Stopwatch(sw => sw.Start()).AsAutoLog(Logger))
            {
                process.WaitForExit();
                if (process.ExitCode != 0) logEntry.Warn();
                logEntry.Message($" {robotFileName.DoubleQuote()} exited with error code {process.ExitCode}.");
            }

            return true;
        }

        private static bool IsRunning(string robotFileName, string arguments)
        {
            return Wmi.IsRunning(arguments, Wmi.GetCommandLines(Path.GetFileName(robotFileName)).ToList());
        }
    }
}
