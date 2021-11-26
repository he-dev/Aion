using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Aion.Data;
using Aion.Services;
using Quartz;
using System.Management;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Reusable.Exceptionize;

namespace Aion.Jobs;

[UsedImplicitly]
[DisallowConcurrentExecution]
internal class WorkflowLauncher : IJob
{
    private readonly ILogger<WorkflowLauncher> _logger;
    private readonly WorkflowReader _workflowReader;

    public WorkflowLauncher(ILogger<WorkflowLauncher> logger, WorkflowReader workflowReader)
    {
        _logger = logger;
        _workflowReader = workflowReader;
    }

    // Executes this job.
    public Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Executing job '{Name}'.", context.JobDetail.Key.Name);

        var workflowName = Path.Combine(context.JobDetail.JobDataMap.GetString(nameof(WorkflowService.Options.WorkflowsDirectory))!, $"{context.JobDetail.Key.Name}.json");
        var workflow = _workflowReader.ReadWorkflow(workflowName);
        Execute(workflow);

        return Task.CompletedTask;
    }

    // Executes job's workflow.
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

    // Executes workflow's steps.
    private void Execute(Step step)
    {
        var fileName = step.FileName;
        //var fileName = WorkflowVersion.FindLatest(step.FileName);
        
        if (!File.Exists(step.FileName))
        {
            throw DynamicException.Create("FileNotFound", $"Step file '{step.FileName}' not found.");
        }

        if (step.SingleInstance && WindowsManagement.IsRunning(step.FileName, step.Arguments))
        {
            _logger.LogInformation("Step '{FileName} {Arguments}' is already running.", step.FileName, step.Arguments);
            return;
        }

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = step.FileName,
            Arguments = step.Arguments,
            WindowStyle = step.WindowStyle,
            UseShellExecute = true,
        });

        _logger.LogInformation("Started step '{FileName} {Arguments}'.", step.FileName, step.Arguments);

        if (process is null || process.HasExited)
        {
            return;
        }

        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            _logger.LogError("Step '{FileName} {Arguments}' exited with error code: {ExitCode}", step.FileName, step.Arguments, process.ExitCode);
        }
        else
        {
            _logger.LogInformation("Step '{FileName} {Arguments}' successfully finished.", step.FileName, step.Arguments);
        }
    }
}