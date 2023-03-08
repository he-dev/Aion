using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Quartz;
using System.Threading.Tasks;
using AionApi.Models;
using AionApi.Services;
using AionApi.Utilities;
using Reusable;
using Reusable.Wiretap;
using Reusable.Wiretap.Abstractions;

namespace AionApi.Jobs;

[DisallowConcurrentExecution]
public class WorkflowRunner : IJob
{
    public WorkflowRunner(ILogger logger, WorkflowStore store, Services.WorkflowScheduler scheduler)
    {
        Logger = logger;
        Store = store;
        Scheduler = scheduler;
    }

    private ILogger Logger { get; }

    private WorkflowStore Store { get; }

    private Services.WorkflowScheduler Scheduler { get; }

    public async Task Execute(IJobExecutionContext context)
    {
        var workflowName = context.JobDetail.Key.Name;
        using var status = Logger.Start("ExecuteJob", new { name = workflowName });

        if (await Store.Get(workflowName) is { } workflow)
        {
            await Run(workflow);
            status.Completed();
        }
        // The workflow-file might have been deleted.
        else
        {
            status.Canceled(new { reason = "Workflow not found." });

            await Scheduler.Delete(workflowName);
            Store.Delete(workflowName);
        }
    }

    public async Task<IEnumerable<AsyncProcess.Result>> Run(Workflow workflow)
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
            fileName = Placeholder.Resolve(fileName, workflow.Variables);
            fileName = LatestVersion.Find(fileName, Directory.EnumerateDirectories);

            status.Running(new { command = i }, fileName);

            var result = await AsyncProcess.StartAsync
            (
                command.FileName,
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