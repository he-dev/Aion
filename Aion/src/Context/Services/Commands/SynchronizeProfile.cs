using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Modules.Scheduler;
using Aion.Modules.Services;
using Aion.Modules.Services.Queries;
using Aion.Toolbox.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Context.Services.Commands;

public class SynchronizeProfile
(
    ILogger<SynchronizeProfile> logger,
    GetProfile getProfile,
    SynchronizeWorkflow synchronizeWorkflow
)
{
    public async Task<IImmutableList<SynchronizeWorkflowResult>> Invoke(string profileName)
    {
        using var activity = new Activity(nameof(SynchronizeProfile)).Start();
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });
        logger.LogInformation("Profile synchronization...");

        var results = ImmutableList<SynchronizeWorkflowResult>.Empty;
        var errors = ImmutableList<Exception>.Empty;

        var profile = getProfile.Where(profileName);
        var workflowMatches = profile.Workflows.All();
        foreach (var workflowPath in workflowMatches)
        {
            try
            {
                var result = await synchronizeWorkflow.Invoke(workflowPath);
                results = results.Add(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to schedule workflow '{WorkflowPath}'.", workflowPath);
                errors = errors.Add(ex);
            }
        }

        activity.SetStatus(ActivityStatusCode.Ok).Stop();
        logger.LogInformation("Profile synchronization finished in {Duration}.", activity.Duration);

        return results;
    }
}