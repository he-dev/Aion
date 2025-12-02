using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Core.Options;
using Aion.Core.Workflows;
using Aion.Util.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Core.Commands.Workflows;

public class SynchronizeWorkflows
(
    ILogger<SynchronizeWorkflows> logger,
    IOptionsSnapshot<SchedulerOptions> schedulerOptions,
    RenderWorkflow renderWorkflow,
    WorkflowScheduleRegistry workflowScheduleRegistry
)
{
    public async Task<IImmutableList<WorkflowSyncResult>> Invoke(string profileName)
    {
        var profile = schedulerOptions.Value.Profiles[profileName];

        using var activity = new Activity("SynchronizingWorkflows").Start();
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });
        logger.LogInformation("Profile synchronization...");

        var results = ImmutableList<WorkflowSyncResult>.Empty;

        var matches = profile.Workflows.All();
        foreach (var match in matches)
        {
            try
            {
                var workflow = await renderWorkflow.For(match);
                results = results.Add(await workflowScheduleRegistry.AddOrUpdate(workflow));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to synchronize workflow '{WorkflowPath}'.", match.Path);
                results = results.Add(new WorkflowSyncResult.Failed
                {
                    Path = match.Path,
                    Exception = ex
                });
            }
        }

        activity.SetStatus(ActivityStatusCode.Ok).Stop();
        logger.LogInformation("Profile synchronization finished in {Duration}.", activity.Duration);

        return results;
    }
}