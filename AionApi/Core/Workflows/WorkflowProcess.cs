using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AionApi.Util;
using AionApi.Utilities;
using Microsoft.Extensions.Logging;

namespace AionApi.Workflows;

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

        logger.LogInformation("Starting workflow '{workflow}'...", workflow.Name);

        foreach (var step in workflow.Steps)
        {
            var indexOrName = step.Name ?? step.Index.ToString();

            if (!step.Enabled)
            {
                logger.LogWarning("Skipping step '{workflow}[{indexOrName}]' because it is disabled.", workflow.Name, indexOrName);
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
                    logger.LogWarning("Aborting step '{workflow}[{indexOrName}]' because it depends on the previous one and it failed.", workflow.Name, indexOrName);
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

            logger.LogInformation("Starting step '{workflow}[{indexOrName}]'...", workflow.Name, indexOrName);

            var result = await asyncProcess.StartAsync(startInfo, step.TimeoutMilliseconds);
            if (result)
            {
                logger.LogInformation(
                    "Completed step '{workflow}[{indexOrName}]' in {seconds:N3} seconds.",
                    workflow.Name, indexOrName, result.Elapsed.TotalSeconds
                );
                if (step.LogStdOut)
                {
                    logger.LogDebug("StdOut: {stdout}", result.Output);
                }
            }
            else
            {
                logger.LogError(
                    result.Exception,
                    "Failed step '{workflow}[{indexOrName}]' with exit code {exitCode} in {seconds:N3} seconds.",
                    workflow.Name, indexOrName, result.ExitCode, result.Elapsed.TotalSeconds
                );
                if (step.LogStdErr)
                {
                    logger.LogError("StdErr: {stderr}", result.Error);
                }
            }

            yield return (step, previousResult = result);
        }

        logger.LogInformation("Completed workflow '{workflow}' in {seconds:N3} seconds.", workflow.Name, stopwatch.Elapsed.TotalSeconds);
    }
}