using System;
using System.Threading.Tasks;
using Aion.Core.Modules;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Jobs;

[UsedImplicitly]
[DisallowConcurrentExecution]
internal class SynchronizationJob
(
    ILogger<SynchronizationJob> logger,
    WorkflowDirectory directory,
    WorkflowSchedule scheduler
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Synchronizing workflows...");

        foreach (var workflowPath in directory.FindFiles(FileFilter.Any, FileExtension.Json))
        {
            try
            {
                var workflow = await Workflow.FromFile(workflowPath);
                await scheduler.Synchronize(workflow);
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