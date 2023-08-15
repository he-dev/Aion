using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AionApi.Models;
using Reusable;
using Reusable.Extensions;
using Reusable.Wiretap;
using Reusable.Wiretap.Abstractions;

namespace AionApi.Services;

public class WorkflowProcess
{
    public WorkflowProcess(ILogger<WorkflowProcess> logger, IAsyncProcess asyncProcess)
    {
        Logger = logger;
        AsyncProcess = asyncProcess;
    }

    private ILogger Logger { get; }

    private IAsyncProcess AsyncProcess { get; }

    public async IAsyncEnumerable<CommandResult> Start(Workflow workflow)
    {
        // cannot log this as the status is going to be disposed on yield
        //using var status = Logger.Start("ExecuteWorkflow", details: new { name = workflow.Name });

        var results = new List<CommandResult>();

        foreach (var (command, index) in workflow.Commands.Select((cmd, i) => (cmd, i)).Where(x => x.cmd))
        {
            var fileName = command.FileName.Format(workflow.Variables.TryGetValue);
            var arguments = command.Arguments.Select(arg => arg.Format(workflow.Variables.TryGetValue)).ToList();
            var workingDirectory = Environment.ExpandEnvironmentVariables(command.WorkingDirectory);
            using var activity = Logger.Begin("ExecuteCommand", details: new { workflow = workflow.Name, command = new { index, fileName, arguments } });

            if (command.DependsOn is { } dependsOn)
            {
                if (dependsOn.Trim().Equals("$previous", StringComparison.OrdinalIgnoreCase) && results.LastOrDefault() is { Process.Success: false })
                {
                    activity.LogBreak(message: "Command depends on the previous one and it failed.");
                    break;
                }

                if (results.SingleOrDefault(r => command.DependsOn.Equals(r.Name, StringComparison.OrdinalIgnoreCase)) is null or { Process.Success: false })
                {
                    activity.LogBreak(message: $"Command depends on '{command.DependsOn}' and it was either not executed or it failed.");
                    break;
                }
            }

            var result = await AsyncProcess.StartAsync(fileName, arguments, workingDirectory, command.TimeoutMilliseconds);
            activity.LogResult(attachment: result);
            if (result)
            {
                activity.LogEnd();
            }
            else
            {
                activity.LogError();
            }

            yield return new CommandResult(command.Name, index, command.FileName, command.Arguments, result).Also(results.Add);
        }
    }

    public record CommandResult(string? Name, int Index, string FileName, IEnumerable<string> Arguments, AsyncProcess.Result Process);
}