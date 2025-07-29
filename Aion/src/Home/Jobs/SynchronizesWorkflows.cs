using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services;
using Aion.Core.Services.Scheduling;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Home.Jobs;

[DisallowConcurrentExecution]
internal class SynchronizesWorkflows
(
    ILogger<SynchronizesWorkflows> logger,
    FindsWorkflows findsWorkflows,
    SynchronizesWorkflowCron synchronizesWorkflowCron
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var profileName = context.JobDetail.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        var profilePath = context.JobDetail.JobDataMap.GetString(JobDataKeys.ProfilePath)!;

        using var activity = new Activity(nameof(SynchronizesWorkflows)).Start();
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });
        logger.LogInformation("Scheduling profile...");

        foreach (var workflowPath in findsWorkflows.Where(profileName, FileFilter.Any))
        {
            try
            {
                var workflow = await Workflow.FromFile(workflowPath);
                var result = await synchronizesWorkflowCron.For(profileName, workflow);
                logger.LogInformation("Workflow '{WorkflowPath}' has been scheduled.", workflowPath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to load '{WorkflowPath}'.", workflowPath);
            }
        }
    }
}
