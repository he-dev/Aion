using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Aion.Meta.Logging;
using Aion.Util;
using Aion.Util.Services;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Services.Downtimes;

public class EndDowntime
(
    ILogger<EndDowntime> logger,
    GetProfile getProfile,
    FindWorkflows findWorkflows
)
{
    public async Task<IImmutableList<WorkflowDowntime>> Now(string profileName, string? pattern = null)
    {
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });

        var profile = getProfile.Single(profileName);
        var workflowDowntimes = new List<WorkflowDowntime>();

        foreach (var workflowPath in findWorkflows.Where(WorkflowSearchCriteria.Where(profile, pattern)))
        {
            try
            {
                if (await WorkflowDowntime.FromFile(workflowPath) is { } workflowDowntime)
                {
                    await workflowDowntime.EndsNow();
                    workflowDowntimes.Add(workflowDowntime);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to end downtime for '{WorkflowName}'.", workflowPath.WorkflowName);
            }
        }

        return workflowDowntimes.ToImmutableList();
    }
}