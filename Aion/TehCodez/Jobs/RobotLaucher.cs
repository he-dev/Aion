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
using System.Management;
using System.Collections.Generic;
using Process = Aion.Data.Process;

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
            if (Scheduler.TryGetProcessGroup(schemeName, out ProcessGroup scheme))
            {
                LaunchRobots(scheme);
            }
        }

        private static void LaunchRobots(ProcessGroup processGroup)
        {
            foreach (var robotConfig in processGroup.Where(r => r.Enabled))
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

        private static void LaunchRobot(string robotsDirectoryName, Process processInfo)
        {
            var latestVersion =
                RobotDirectory
                    .GetVersions(RobotPath.Combine(robotsDirectoryName, processInfo.FileName))
                    .GetLatestVersion();

            var robotFileName =
                new RobotFileNameBuilder()
                    .RobotDirectoryName(robotsDirectoryName)
                    .Version(latestVersion)
                    .FileName(processInfo.FileName)
                    .Build();

            if (string.IsNullOrEmpty(robotFileName))
            {
                throw new FileNotFoundException($"Robot not found '{robotFileName}'.");
            }

            if (IsRunning(processInfo.FileName, processInfo.Arguments))
            {
                throw new InvalidOperationException($"Robot already running '{robotFileName}'.");
            }

            using (var process = System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = robotFileName,
                Arguments = processInfo.Arguments,
                WindowStyle = processInfo.WindowStyle,
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
            var commandLines = GetCommandLines(robotFileName).ToList();  // Wmi.GetCommandLines(Path.GetFileName(robotFileName)).ToList();

            // Skip the file name.
            var currentCommandLines = commandLines.Select(x => Reusable.Colin.Services.CommandLineTokenizer.Tokenize(x).Skip(1).OrderBy(y => y)).ToList();
            if (!currentCommandLines.Any()) return false;

            var tokens = Reusable.Colin.Services.CommandLineTokenizer.Tokenize(arguments ?? string.Empty).OrderBy(x => x).ToList();
            return currentCommandLines.Any(x => x.SequenceEqual(tokens));
        }

        private static IEnumerable<string> GetCommandLines(string processName)
        {
            //if (processName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(processName));

            var query = $"SELECT CommandLine FROM Win32_Process WHERE Name = '{processName}'";
            using (var searcher = new ManagementObjectSearcher(query))
            using (var results = searcher.Get())
            {
                foreach (var instance in results)
                {
                    yield return instance["CommandLine"].ToString();
                }
            }
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
