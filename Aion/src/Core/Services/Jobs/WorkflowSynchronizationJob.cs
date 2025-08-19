using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Core.Entities;
using Aion.Util.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Core.Services.Jobs;

[DisallowConcurrentExecution]
internal class WorkflowSynchronizationJob
(
    ILogger<WorkflowSynchronizationJob> logger,
    IOptions<InstanceOptions> engineOptions,
    WorkflowRendering workflowRendering,
    WorkflowScheduleRegistry workflowScheduleRegistry
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var profileName = context.Trigger.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        var profile = engineOptions.Value[profileName];
        using var activity = new Activity("SynchronizingWorkflows").Start();
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });
        logger.LogInformation("Scheduling profile...");

        var matches = profile.Workflows.All();
        foreach (var match in matches)
        {
            try
            {
                var workflow = await workflowRendering.RenderFrom(match);
                await workflowScheduleRegistry.AddOrUpdate(workflow);
                logger.LogInformation("Workflow '{WorkflowPath}' has been scheduled.", match.Path);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to load '{WorkflowPath}'.", match.Path);
            }
        }
    }
}
