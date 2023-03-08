using System;
using System.IO;
using System.Linq;
using Aion.Data;
using Aion.Services;
using Quartz;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

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
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Executing job '{Name}'", context.JobDetail.Key.Name);

        var workflowName = Path.Combine(context.JobDetail.JobDataMap.GetString(nameof(WorkflowService.Options.WorkflowsDirectory))!, context.JobDetail.Key.Name);
        try
        {
            var workflow = _workflowReader.ReadWorkflow(workflowName);
            await Execute(workflow, context.CancellationToken);
        }
        catch (Exception e)
        {
            
        }
    }

    // Executes job's workflow.
    private async Task Execute(Workflow workflow, CancellationToken cancellationToken)
    {
        foreach (var step in workflow.Steps.Where(s => s.Enabled))
        {
            var result = await ProcessHelper.StartAsync
            (
                step.FileName,
                step.Arguments ?? string.Empty,
                step.WorkingDirectory ?? string.Empty,
                step.TimeoutInMilliseconds
            );

            if (result.Success)
            {
                _logger.LogInformation("Step '{Name}' successfully finished:{NewLine} {Output}", step.Name, Environment.NewLine, result.Output);
            }
            else
            {
                _logger.LogError("Step '{Name}' failed with error code {ExitCode}:{NewLine} {Error}", step.Name, result.ExitCode, Environment.NewLine, result.Error);
                _logger.LogError(result.Exception, "Exception:{NewLine} {Exception}", Environment.NewLine, result.Exception);

                if (step.OnError == OnError.Break)
                {
                    break;
                }
            }
        }
    }
}