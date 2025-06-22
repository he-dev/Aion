using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Aion.Util;
using Aion.Utilities;
using Microsoft.Extensions.Logging;

namespace Aion.Workflows;

public class WorkflowProcess
(
    ILogger<WorkflowProcess> logger,
    IAsyncProcess asyncProcess
)
{
    public async IAsyncEnumerable<ValueTuple<Workflow.Step, AsyncProcess.Result>> Start(Workflow workflow, params VariableGroup[] variableGroups)
    {
        var workflowVariables = variableGroups.Append(new VariableGroup("var", workflow.Variables)).ToList();
        var previousResult = default(AsyncProcess.Result);
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Workflow '{workflow}' starting...", workflow.Name);

        foreach (var step in workflow.Steps)
        {
            var indexOrName = step.Name ?? step.Index.ToString();

            if (!step.Enabled)
            {
                logger.LogWarning("Step '{workflow}[{indexOrName}]' skipped because it is disabled.", workflow.Name, indexOrName);
                continue;
            }

            var stepVariables = new VariableGroup("step")
            {
                ["name"] = step.Name,
                ["index"] = step.Index,
                //["dependsOn"] = step.DependsOn,
                //["timeoutMilliseconds"] = step.TimeoutMilliseconds,
            };

            workflowVariables = workflowVariables.Append(stepVariables).ToList();

            var fileName = VariableTemplate.Render(step.Script, workflowVariables);
            var arguments = step.Args.Select(arg => VariableTemplate.Render(arg, workflowVariables)).ToList();
            var workingDirectory = VariableTemplate.Render(step.WorkingDirectory ?? string.Empty, workflowVariables);

            if (step.DependsOn is { } dependsOn)
            {
                if (dependsOn.Trim().Equals("$previous", StringComparison.OrdinalIgnoreCase) && previousResult is { ExitCode: not 0 })
                {
                    logger.LogWarning("Step '{workflow}[{indexOrName}]' aborted because it depends on the previous one and it failed.", workflow.Name, indexOrName);
                    break;
                }
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = string.Join(' ', arguments.Select(a => a.Trim())),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = !step.WindowVisible,
                WorkingDirectory = workingDirectory
            };

            logger.LogDebug
            (
                "Step '{workflow}[{indexOrName}]' will run: '{fileName} {arguments}'.",
                workflow.Name, indexOrName, fileName, arguments
            );

            var result = await asyncProcess.StartAsync(startInfo, step.TimeoutMilliseconds);
            if (result)
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

            yield return (step, previousResult = result);
        }

        logger.LogInformation("Workflow '{workflow}' completed in {elapsed}.", workflow.Name, stopwatch.Elapsed);
    }
}