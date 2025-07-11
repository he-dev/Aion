using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Util;
using Aion.Util.Json;
using Aion.Util.Scriban;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Modules;

// Executes workflow's enabled steps.
public class WorkflowProcess
(
    ILogger<WorkflowProcess> logger,
    WorkflowSink workflowSink
)
{
    public async Task Start(Workflow workflow, IImmutableList<VariableGroup> variables)
    {
        variables = variables.Add(new LocalVariableGroup(workflow.Variables));

        // meta: Bracket style to avoid unnecessary variables.
        using (workflowSink.Push(workflow.Name, workflow.Serilog, variables))
        {
            var previousExitCode = default(int?);
            var stopwatch = Stopwatch.StartNew();

            logger.LogInformation("Executing workflow...");

            // note: Does not filter out disabled steps because we want them logged.
            foreach (var template in workflow.Steps)
            {
                var stepVariables = variables.Add(new StepVariableGroup
                {
                    Name = template.Name,
                    Index = template.Index,
                });

                var step = template.RenderVariables(stepVariables);

                using (logger.BeginScopeFrom(new { StepIndex = step.Index, StepName = step.Name }))
                {
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
                        previousExitCode = await Execute(step, variables);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Step failed with an exception.");
                        previousExitCode = null;
                    }
                }
            }

            logger.LogInformation("Workflow completed in {Elapsed}.", stopwatch.Elapsed);
        }
    }

    private async Task<int> Execute(Workflow.Step step, IImmutableList<VariableGroup> variables)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = step.Script,
            Arguments = string.Join(' ', step.Args.Select(a => a.Trim())),
            CreateNoWindow = !step.WindowVisible,
            WorkingDirectory = step.WorkingDirectory
        };

        using var stepLoggerFactory = step.Serilog.RenderPaths(variables).ToLogger().ToLoggerFactory();

        logger.LogInformation("Executing step...");
        var asyncProcess = new AsyncProcess
        {
            FileName = startInfo.FileName,
            Arguments = string.Join(' ', step.Args.Select(a => a.Trim())),
            Logger = stepLoggerFactory.CreateLogger(nameof(Workflow.Step)),
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