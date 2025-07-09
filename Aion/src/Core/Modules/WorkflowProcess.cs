using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aion.Util;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Modules;

// Executes workflow's enabled steps.
public class WorkflowProcess
(
    ILogger<WorkflowProcess> logger
)
{
    public async Task Start(Workflow workflow, WorkflowVariableGroup workflowVariables)
    {
        var applicationVariables = new ApplicationVariableGroup();
        var localVariables = new LocalVariableGroup(workflow.Variables);
        var previousExitCode = default(int?);

        // clue: Does not filter out disabled steps because we want them logged.
        var steps =
            from step in workflow.Steps
            let stepVariables = new StepVariableGroup
            {
                Name = step.Name,
                Index = step.Index,
            }
            select step.RenderVariables
            (
                applicationVariables,
                workflowVariables,
                localVariables,
                stepVariables
            );

        using var workflowScope = logger.BeginScopeFrom(new { WorkflowName = workflow.Name });
        logger.LogInformation("Starting workflow.");

        var stopwatch = Stopwatch.StartNew();
        foreach (var step in steps)
        {
            using var stepScope = logger.BeginScopeFrom(new { StepIndex = step.Index, StepName = step.Name });

            if (!step.Enabled)
            {
                logger.LogWarning("Step skipped because it is disabled.");
                continue;
            }

            if (step is { Index: > 0, DependsOn: "$previous" } && previousExitCode is not 0)
            {
                logger.LogWarning("Workflow aborted because this step depends on the previous one and it failed.");
                break;
            }

            try
            {
                previousExitCode = await Execute(step);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Step failed with an exception.");
                previousExitCode = null;
            }
        }

        logger.LogInformation("Workflow completed in {Elapsed}.", stopwatch.Elapsed);
    }

    private async Task<int> Execute(Workflow.Step step)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = step.Script,
            Arguments = string.Join(' ', step.Args.Select(a => a.Trim())),
            CreateNoWindow = !step.WindowVisible,
            WorkingDirectory = step.WorkingDirectory
        };

        logger.LogDebug("Executing step.");

        await using var stdWriter = step.LogStdTo switch
        {
            { } path => new StreamWriter(path: path, append: true, encoding: Encoding.UTF8) { AutoFlush = true },
            _ => null
        };

        var asyncProcess = new AsyncProcess
        {
            FileName = startInfo.FileName,
            Arguments = string.Join(' ', step.Args.Select(a => a.Trim())),
            StdStream = stdWriter,
        };

        var stopwatch = Stopwatch.StartNew();
        var exitCode = await asyncProcess.StartAsync(step.Timeout);
        switch (exitCode)
        {
            case 0: logger.LogInformation("Step completed in {Elapsed}.", stopwatch.Elapsed); break;
            default: logger.LogError("Step failed with exit code {ExitCode} in {Elapsed}.", exitCode, stopwatch.Elapsed); break;
        }

        return exitCode;
    }
}