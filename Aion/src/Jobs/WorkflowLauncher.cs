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
using Reusable.Exceptionize;

namespace Aion.Jobs
{
    [UsedImplicitly]
    [DisallowConcurrentExecution]
    internal class WorkflowLauncher : IJob
    {
        private readonly ILogger<WorkflowLauncher> _logger;
        private readonly WorkflowReader _workflowReader;
        private readonly IScheduler _scheduler;

        public WorkflowLauncher(ILogger<WorkflowLauncher> logger, WorkflowReader workflowReader, IScheduler scheduler)
        {
            _logger = logger;
            _workflowReader = workflowReader;
            _scheduler = scheduler;
        }

        public Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("Executing job '{Name}'.", context.JobDetail.Key.Name);

            var workflowName = Path.Combine(context.JobDetail.JobDataMap.GetString(nameof(WorkflowService.Options.WorkflowsDirectory))!, $"{context.JobDetail.Key.Name}.json");
            var workflow = _workflowReader.ReadWorkflow(workflowName);
            Execute(workflow);

            return Task.CompletedTask;
        }

        private void Execute(Workflow workflow)
        {
            foreach (var step in workflow.Steps.Where(s => s.Enabled))
            {
                try
                {
                    Execute(step);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing step '{FileName}'.", step.FileName);
                    if (step.OnError == OnError.Break)
                    {
                        break;
                    }
                }
            }
        }

        private void Execute(Step step)
        {
            // var latestVersion =
            //     RobotDirectory
            //         .GetVersions(RobotPath.Combine(robotsDirectoryName, step.FileName))
            //         .GetLatestVersion();
            //
            // var robotFileName =
            //     new RobotFileNameBuilder()
            //         .RobotDirectoryName(robotsDirectoryName)
            //         .Version(latestVersion)
            //         .FileName(step.FileName)
            //         .Build();

            if (!File.Exists(step.FileName))
            {
                throw DynamicException.Create("FileNotFound", $"Step file '{step.FileName}' not found.");
            }
            
            if (IsRunning(step.FileName, step.Arguments))
            {
                throw new InvalidOperationException($"Robot already running '{robotFileName}'.");
            }

            using (var process = System.Diagnostics.Process.Start(new ProcessStartInfo
                   {
                       FileName = robotFileName,
                       Arguments = step.Arguments,
                       WindowStyle = step.WindowStyle,
                       UseShellExecute = true,
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
            var commandLines = GetCommandLines(robotFileName).ToList(); // Wmi.GetCommandLines(Path.GetFileName(robotFileName)).ToList();

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
}