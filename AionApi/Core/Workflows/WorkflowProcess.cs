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
    public async IAsyncEnumerable<StepResult> Start(Workflow workflow)
    {
        var previous = default(StepResult);

        foreach (var step in workflow.Steps)
        {
            var indexOrName = step.Name ?? step.Index.ToString();

            if (!step.Enabled)
            {
                logger.LogWarning("Skipping '{workflow}[{indexOrName}]' because it's disabled.", workflow.Name, indexOrName);
                continue;
            }

            var fileName = VariableTemplate.Render(step.Script, workflow.Variables);
            var arguments = step.Args.Select(arg => VariableTemplate.Render(arg, workflow.Variables)).ToList();
            var workingDirectory = VariableTemplate.Render(step.WorkingDirectory ?? string.Empty, workflow.Variables);

            if (step.DependsOn is { } dependsOn)
            {
                if (dependsOn.Trim().Equals("$previous", StringComparison.OrdinalIgnoreCase) && previous is { ExitCode: not 0 })
                {
                    logger.LogWarning("Aborting '{workflow}[{indexOrName}]' as it depends on the previous one and it failed.", workflow.Name, indexOrName);
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

            logger.LogInformation("Starting '{workflow}[{indexOrName}]'...", workflow.Name, indexOrName);

            var result = await asyncProcess.StartAsync(startInfo, step.TimeoutMilliseconds);
            if (result)
            {
                logger.LogInformation("Completed '{workflow}[{indexOrName}]'.", workflow.Name, indexOrName);
            }
            else
            {
                logger.LogError(result.Exception, "Failed '{workflow}[{indexOrName}]' with exit code {exitCode}.", workflow.Name, indexOrName, result.ExitCode);
            }

            yield return previous = new StepResult(step, result.ExitCode);
        }
    }

    public record StepResult(Workflow.Step Step, int ExitCode);
}