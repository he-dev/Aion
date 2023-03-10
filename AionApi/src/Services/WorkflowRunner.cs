using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AionApi.Models;
using AionApi.Utilities;
using Reusable;
using Reusable.Wiretap;
using Reusable.Wiretap.Abstractions;

namespace AionApi.Services;

public class WorkflowRunner
{
    public WorkflowRunner(ILogger logger, IAsyncProcess asyncProcess, EnumerateDirectoriesFunc enumerateDirectories)
    {
        Logger = logger;
        AsyncProcess = asyncProcess;
        EnumerateDirectories = enumerateDirectories;
    }

    private ILogger Logger { get; }

    private IAsyncProcess AsyncProcess { get; }

    private EnumerateDirectoriesFunc EnumerateDirectories { get; }

    public async Task<IList<AsyncProcess.Result>> Run(Workflow workflow)
    {
        using var status = Logger.Start("RunWorkflow", new { name = workflow.Name });

        var results = new List<AsyncProcess.Result>();

        foreach (var (command, i) in workflow.Commands.Select((cmd, i) => (cmd, i)).Where(x => x.cmd))
        {
            using var commandStatus = Logger.Start("RunCommand", new { command = i });

            if (command.DependsOnPrevious && !(results.LastOrDefault() ?? true))
            {
                status.Canceled(new { reason = "Command depends on the previous one and it faulted." });
                break;
            }

            var fileName = command.FileName;
            fileName = fileName.Format(workflow.Variables);
            fileName = LatestVersion.Find(fileName, EnumerateDirectories);

            status.Running(new { command = i }, fileName);

            var result = await AsyncProcess.StartAsync
            (
                fileName,
                command.Arguments,
                command.WorkingDirectory,
                command.TimeoutMilliseconds
            );
            results.Add(result);

            Action<object?, object?> finalize = result ? commandStatus.Completed : commandStatus.Faulted;
            finalize(new { result.ExitCode, result.Success, result.Completed, result.Killed }, result);
        }

        return results;
    }
}