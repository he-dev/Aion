using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Modules.Scheduler;
using Aion.Toolbox.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Premise.Services.Commands;

public class SynchronizeProfile
(
    ILogger<SynchronizeProfile> logger,
    IOptionsSnapshot<SchedulerOptions> schedulerOptions,
    ScheduleWorkflow scheduleWorkflow
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
        foreach (var workflowPath in workflowMatches)
        {
            try
            {
                var result = await scheduleWorkflow.Invoke(workflowPath);
                results = results.Add(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to schedule workflow '{WorkflowPath}'.", workflowPath);
                results = results.Add(new WorkflowSyncResult.Failed
                {
                    Path = workflowPath,
                    Exception = ex
                });
            }
        }

        activity.SetStatus(ActivityStatusCode.Ok).Stop();
        logger.LogInformation("Profile synchronization finished in {Duration}.", activity.Duration);

        return results;
    }
}