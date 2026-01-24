using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Meta.Logging;
using Aion.Util;
using Aion.Util.Services;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Services;

public class EndDowntime
(
    ILogger<EndDowntime> logger,
    GetProfile getProfile,
    FindWorkflows findWorkflows
)
{
    public async Task<object> Now(string profileName, string? pattern = null)
    {
        var profile = getProfile.Single(profileName);
        var workflowPaths = ImmutableList<WorkflowPath>.Empty;

        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });

        foreach (var workflowPath in findWorkflows.Where(WorkflowSearchCriteria.Where(profile, pattern)))
        {
            try
            {
                if (await WorkflowDowntime.FromFile(workflowPath) is { } lockFile)
                {
                    await lockFile.EndsNow();
                    workflowPaths = workflowPaths.Add(workflowPath);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to end downtime for '{WorkflowName}'.", workflowPath.WorkflowName);
            }
        }

        return new
        {
            profile = profile.Path,
            workflows = workflowPaths.Select(m => m.WorkflowName.ToPath()),
        };
    }
}