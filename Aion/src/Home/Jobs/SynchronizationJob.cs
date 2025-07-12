using System;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Util.Serilog;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Home.Jobs;

[UsedImplicitly]
[DisallowConcurrentExecution]
internal class SynchronizationJob
(
    ILogger<SynchronizationJob> logger,
    WorkflowDirectory workflowDirectory,
    WorkflowScheduler workflowScheduler
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Synchronizing workflows...");

        var executionId = Guid.NewGuid().ToString("D");

        foreach (var workflowPath in workflowDirectory.FindFiles(FileFilter.Any, FileExtension.Json))
        {
            try
            {
                var workflow = await Workflow.FromFile(workflowPath);
                using var scope = logger.BeginScopeFrom(new { ExecutionId = executionId });
                await workflowScheduler.Synchronize(workflow);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error synchronizing workflow '{workflowPath}'.", workflowPath);
            }
        }
    }

    public static IJobDetail CreateJobDetail() =>
        JobBuilder
            .Create<SynchronizationJob>()
            .WithIdentity("workflow-synchronization", JobGroupNames.Services)
            .Build();
}