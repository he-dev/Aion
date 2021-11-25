using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Aion.Data;
using Aion.Services;
using Quartz;
using System.Management;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Aion.Jobs
{
    [UsedImplicitly]
    [DisallowConcurrentExecution]
    internal class WorkflowLauncher : IJob
    {
        private readonly ILogger<WorkflowLauncher> _logger;
        private readonly IScheduler _scheduler;

        public WorkflowLauncher(ILogger<WorkflowLauncher> logger, IScheduler scheduler)
        {
            _logger = logger;
            _scheduler = scheduler;
        }

        public Task Execute(IJobExecutionContext context)
        {
            var schemeName = context.JobDetail.Key.Name;

            _logger.LogInformation("Executing job '{Name}'.", context.JobDetail.Key.Name);

            // if (_scheduler.TryGetProcessGroup(schemeName, out var scheme))
            // {
            //     LaunchRobots(scheme);
            // }

            return Task.CompletedTask;
        }

        // private static void LaunchRobots(Workflow workflow)
        // {
        //     foreach (var robotConfig in workflow.Where(r => r.Enabled))
        //     {
        //         try
        //         {
        //             LaunchRobot(Program.Configuration.Load<Program, Global>().RobotsDirectoryName, robotConfig);
        //         }
        //         catch (Exception ex)
        //         {
        //             LogEntry.New().Error().Exception(ex).Message($"Error starting '{robotConfig.FileName}'.").Log(Logger);
        //             break;
        //         }
        //     }
        // }
        //
        // private static void LaunchRobot(string robotsDirectoryName, Step stepInfo)
        // {
        //     var latestVersion =
        //         RobotDirectory
        //             .GetVersions(RobotPath.Combine(robotsDirectoryName, stepInfo.FileName))
        //             .GetLatestVersion();
        //
        //     var robotFileName =
        //         new RobotFileNameBuilder()
        //             .RobotDirectoryName(robotsDirectoryName)
        //             .Version(latestVersion)
        //             .FileName(stepInfo.FileName)
        //             .Build();
        //
        //     if (string.IsNullOrEmpty(robotFileName))
        //     {
        //         throw new FileNotFoundException($"Robot not found '{robotFileName}'.");
        //     }
        //
        //     if (IsRunning(stepInfo.FileName, stepInfo.Arguments))
        //     {
        //         throw new InvalidOperationException($"Robot already running '{robotFileName}'.");
        //     }
        //
        //     using (var process = System.Diagnostics.Process.Start(new ProcessStartInfo
        //            {
        //                FileName = robotFileName,
        //                Arguments = stepInfo.Arguments,
        //                WindowStyle = stepInfo.WindowStyle,
        //                //UseShellExecute = false,
        //            }))
        //     {
        //         if (process == null)
        //         {
        //             throw new ProcessNotStartedException(robotFileName);
        //         }
        //
        //         LogEntry.New().Info().Message($"Started '{robotFileName}'").Log(Logger);
        //
        //         using (var logEntry = LogEntry.New().Info().Stopwatch(sw => sw.Start()).AsAutoLog(Logger))
        //         {
        //             process.WaitForExit();
        //             if (process.ExitCode != 0)
        //             {
        //                 logEntry.Error().Message($"'{robotFileName}' exited with error code {process.ExitCode}.");
        //                 throw new ProcessTerminatedException($"'{robotFileName}' exited with error code {process.ExitCode}.");
        //             }
        //
        //             logEntry.Info().Message($"'{robotFileName}' exited with error code {process.ExitCode}.");
        //         }
        //     }
        // }
        //
        // private static bool IsRunning(string robotFileName, string arguments)
        // {
        //     var commandLines = GetCommandLines(robotFileName).ToList(); // Wmi.GetCommandLines(Path.GetFileName(robotFileName)).ToList();
        //
        //     // Skip the file name.
        //     var currentCommandLines = commandLines.Select(x => Reusable.Colin.Services.CommandLineTokenizer.Tokenize(x).Skip(1).OrderBy(y => y)).ToList();
        //     if (!currentCommandLines.Any()) return false;
        //
        //     var tokens = Reusable.Colin.Services.CommandLineTokenizer.Tokenize(arguments ?? string.Empty).OrderBy(x => x).ToList();
        //     return currentCommandLines.Any(x => x.SequenceEqual(tokens));
        // }
        //
        // private static IEnumerable<string> GetCommandLines(string processName)
        // {
        //     //if (processName.IsNullOrEmpty()) throw new ArgumentNullException(nameof(processName));
        //
        //     var query = $"SELECT CommandLine FROM Win32_Process WHERE Name = '{processName}'";
        //     using (var searcher = new ManagementObjectSearcher(query))
        //     using (var results = searcher.Get())
        //     {
        //         foreach (var instance in results)
        //         {
        //             yield return instance["CommandLine"].ToString();
        //         }
        //     }
        // }
    }
}