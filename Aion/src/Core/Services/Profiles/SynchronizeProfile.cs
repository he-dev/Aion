using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;
using Aion.Core.Services.Workflows;
using Aion.Meta.Logging;
using Aion.Util;
using Aion.Util.Services;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Services.Profiles;

public class SynchronizeProfile
(
    ILogger<SynchronizeProfile> logger,
    GetProfile getProfile,
    FindWorkflows findWorkflows,
    SynchronizeWorkflow synchronizeWorkflow
)
{
    // meta: Cannot be IAsyncEnumerable because of try/catch.
    public async Task<IImmutableList<SynchronizeWorkflowResult>> Invoke(string profileName)
    {
        using var activity = new Activity(nameof(SynchronizeProfile)).Start();
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });
        logger.LogInformation("Synchronizing profile '{ProfileName}'.", profileName);

        try
        {
            var results = ImmutableList<SynchronizeWorkflowResult>.Empty;
            var profile = getProfile.Single(profileName);
            var workflowPaths = findWorkflows.Where(WorkflowSearchCriteria.Where(profile));
            foreach (var workflowPath in workflowPaths)
            {
                var result = await synchronizeWorkflow.Invoke(workflowPath);
                results = results.Add(result);
            }

            activity.SetStatus(ActivityStatusCode.Ok).Stop();
            logger.LogInformation("Profile '{ProfileName}' synchronization finished in {Duration:N0}.", profileName, activity.Duration);

            return results;
        }
        catch (Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error).Stop();
            logger.LogError(ex, "Profile '{ProfileName}' synchronization failed.", profileName);
            throw;
        }
    }
}