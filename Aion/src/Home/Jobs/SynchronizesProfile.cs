using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Modules;
using Aion.Core.Providers;
using Aion.Core.Schedulers;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Home.Jobs;

[DisallowConcurrentExecution]
internal class SynchronizesProfile
(
    ILogger<SynchronizesProfile> logger,
    FindsWorkflows findsWorkflows,
    SchedulesWorkflowExecution schedulesWorkflowExecution
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var profileName = context.JobDetail.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        var profilePath = context.JobDetail.JobDataMap.GetString(JobDataKeys.ProfilePath)!;

        using var activity = new Activity(nameof(SynchronizesProfile)).Start();
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });
        logger.LogInformation("Synchronizing profile...");

        foreach (var workflowPath in findsWorkflows.Where(profileName, FileFilter.Any, FileExtension.Json))
        {
            try
            {
                var workflow = await Workflow.FromFile(workflowPath);
                await schedulesWorkflowExecution.For(workflow, profileName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to synchronize workflow '{WorkflowPath}'.", workflowPath);
            }
        }
    }
}

public record SynchronizationJobOptions
{
    public string Cron { get; init; } = null!;

    public bool IsOn { get; init; } = true;
}