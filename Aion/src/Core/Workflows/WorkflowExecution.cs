using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Util;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Workflows;

// Executes workflow's enabled steps.
public class WorkflowExecution
(
    ILogger<WorkflowExecution> logger,
    IAsyncProcess asyncProcess
)
{
    public async Task Start(Workflow workflow, WorkflowVariableGroup workflowVariables)
    {
        var applicationVariables = new ApplicationVariableGroup();
        var localVariables = new LocalVariableGroup(workflow.Variables);

        var previousResult = default(AsyncProcess.Result);
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Workflow '{workflow}' starting...", workflow.Name);

        var steps =
            from step in workflow.Steps
            // !! Do not filter disabled steps here. We want them logged.
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

        foreach (var step in steps)
        {
            var indexOrName = step.Name ?? step.Index.ToString();

            if (!step.Enabled)
            {
                logger.LogWarning("Step '{workflow}[{indexOrName}]' skipped because it is disabled.", workflow.Name, indexOrName);
                continue;
            }

            if (step.DependsOn is { } dependsOn)
            {
                if (dependsOn.Trim().Equals("$previous", StringComparison.OrdinalIgnoreCase) && previousResult is { ExitCode: not 0 })
                {
                    logger.LogWarning
                    (
                        "Step '{workflow}[{indexOrName}]' aborted because it depends on the previous one and it failed.",
                        workflow.Name, indexOrName
                    );
                    break;
                }
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = step.Script,
                Arguments = string.Join(' ', step.Args.Select(a => a.Trim())),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = !step.WindowVisible,
                WorkingDirectory = step.WorkingDirectory
            };

            logger.LogDebug
            (
                "Step '{workflow}[{indexOrName}]' will run: '{fileName} {arguments}'.",
                workflow.Name, indexOrName, startInfo.FileName, startInfo.Arguments
            );

            if (await asyncProcess.StartAsync(startInfo, step.TimeoutMilliseconds) is var result && result)
            {
                logger.LogInformation
                (
                    "Step '{workflow}[{indexOrName}]' completed in {elapsed}.",
                    workflow.Name, indexOrName, result.Elapsed
                );
                if (step.LogStdOut)
                {
                    logger.LogDebug("StdOut: {stdout}", result.StdOut);
                }
            }
            else
            {
                logger.LogError
                (
                    result.Exception,
                    "Step '{workflow}[{indexOrName}]' failed with exit code {exitCode} in {elapsed}.",
                    workflow.Name, indexOrName, result.ExitCode, result.Elapsed
                );
                if (step.LogStdErr)
                {
                    logger.LogError("StdErr: {stderr}", result.StdErr);
                }
            }
        }

        logger.LogInformation("Workflow '{workflow}' completed in {elapsed}.", workflow.Name, stopwatch.Elapsed);
    }
}