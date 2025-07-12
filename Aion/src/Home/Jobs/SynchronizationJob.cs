using System;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Util.Serilog;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Home.Jobs;

[UsedImplicitly]
[DisallowConcurrentExecution]
internal class SynchronizationJob
(
    ILogger<SynchronizationJob> logger,
    IOptions<SynchronizationJobOptions> options,
    ISchedulerFactory schedulerFactory,
    WorkflowDirectory workflowDirectory,
    WorkflowScheduler workflowScheduler
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (options.Value.Disabled)
        {
            logger.LogWarning("Synchronization job is disabled, so it will be deleted.");
            var scheduler = await schedulerFactory.GetScheduler();
            await scheduler.DeleteJob(context.JobDetail.Key);

            return;
        }

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