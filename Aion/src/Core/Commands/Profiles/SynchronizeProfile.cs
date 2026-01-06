using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Core.Commands.Workflows;
using Aion.Core.Options;
using Aion.Core.Workflows;
using Aion.Util.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Core.Commands.Profiles;

public class SynchronizeProfile
(
    ILogger<SynchronizeProfile> logger,
    IOptionsSnapshot<SchedulerOptions> schedulerOptions,
    RenderWorkflow renderWorkflow,
    WorkflowScheduleRegistry workflowScheduleRegistry
)
{
    public async Task<IImmutableList<WorkflowSyncResult>> Invoke(string profileName)
    {
        var profile = schedulerOptions.Value.Profiles[profileName];

        using var activity = new Activity(nameof(SynchronizeProfile)).Start();
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });
        logger.LogInformation("Profile synchronization...");

        var results = ImmutableList<WorkflowSyncResult>.Empty;

        var workflowMatches = profile.Workflows.All();
        foreach (var workflowMatch in workflowMatches)
        {
            try
            {
                var workflow = await renderWorkflow.For(workflowMatch);
                var workflowSyncResult = await workflowScheduleRegistry.AddOrUpdate(workflow);
                results = results.Add(workflowSyncResult);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to schedule workflow '{WorkflowPath}'.", workflowMatch.WorkflowPath);
                results = results.Add(new WorkflowSyncResult.Failed
                {
                    Path = workflowMatch.WorkflowPath,
                    Exception = ex
                });
            }
        }

        activity.SetStatus(ActivityStatusCode.Ok).Stop();
        logger.LogInformation("Profile synchronization finished in {Duration}.", activity.Duration);

        return results;
    }
}