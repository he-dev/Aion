using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Aion.Data;
using Aion.Data.Configuration;
using Aion.Extensions;
using Aion.Services;
using Quartz;
using Reusable.Logging;
using Reusable.Management;

namespace Aion.Jobs
{
    [DisallowConcurrentExecution]
    internal class RobotLaucher : RobotJob, IJob
    {
        private static readonly ILogger Logger = LoggerFactory.CreateLogger(nameof(RobotLaucher));

        public void Execute(IJobExecutionContext context)
        {
            if (Scheduler == null) { throw new InvalidOperationException($"Did you forget to set the '{nameof(Scheduler)}'?"); }

            var schemeName = context.JobDetail.Key.Name;
            if (Scheduler.TryGetRobotScheme(schemeName, out RobotScheme scheme))
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
                    LaunchRobot(Program.Configuration.Load<Program, Global>().RobotsDirectoryName, robotConfig);
                }
                catch (Exception ex)
                {
                    LogEntry.New().Error().Exception(ex).Message($"Error starting '{robotConfig.FileName}'.").Log(Logger);
                    break;
                }
            }
        }

        private static void LaunchRobot(string robotsDirectoryName, RobotInfo robot)
        {
            var latestVersion =
                RobotDirectory
                    .GetVersionDirectories(RobotDirectory.CreateMainDirectoryName(robotsDirectoryName, robot.FileName))
                    .GetLatestVersion();

            var robotFileName =
                new RobotFileNameBuilder()
                    .RobotDirectoryName(robotsDirectoryName)
                    .Version(latestVersion)
                    .FileName(robot.FileName)
                    .Build();

            if (string.IsNullOrEmpty(robotFileName))
            {
                throw new FileNotFoundException($"Robot not found '{robotFileName}'.");
            }

            if (IsRunning(robot.FileName, robot.Arguments))
            {
                throw new InvalidOperationException($"Robot already running '{robotFileName}'.");
            }

            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = robotFileName,
                Arguments = robot.Arguments,
                WindowStyle = robot.WindowStyle,
                //UseShellExecute = false,
            }))
            {
                if (process == null)
                {
                    throw new ProcessNotStartedException(robotFileName);
                }

                LogEntry.New().Info().Message($"Started '{robotFileName}'").Log(Logger);

                using (var logEntry = LogEntry.New().Info().Stopwatch(sw => sw.Start()).AsAutoLog(Logger))
                {
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        logEntry.Error().Message($"'{robotFileName}' exited with error code {process.ExitCode}.");
                        throw new ProcessTerminatedException($"'{robotFileName}' exited with error code {process.ExitCode}.");
                    }
                    logEntry.Info().Message($"'{robotFileName}' exited with error code {process.ExitCode}.");
                }
            }
        }

        private static bool IsRunning(string robotFileName, string arguments)
        {
            var commandLines = Wmi.GetCommandLines(Path.GetFileName(robotFileName)).ToList();

            // Skip the file name.
            var currentCommandLines = commandLines.Select(x => Reusable.Colin.CommandLineTokenizer.Tokenize(x).Skip(1).OrderBy(y => y)).ToList();
            if (!currentCommandLines.Any()) return false;

            var tokens = Reusable.Colin.CommandLineTokenizer.Tokenize(arguments ?? string.Empty).OrderBy(x => x).ToList();
            return currentCommandLines.Any(x => x.SequenceEqual(tokens));
        }
    }

    [Serializable]
    internal class ProcessTerminatedException : Exception
    {
        public ProcessTerminatedException(string message) : base(message, null) { }
    }

    internal class ProcessNotStartedException : Exception
    {
        public ProcessNotStartedException(string fileName)
            : base($"Error starting '{fileName}'.", null)
        { }
    }
}
