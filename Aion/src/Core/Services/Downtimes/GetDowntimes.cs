using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Aion.Meta.Logging;
using Aion.Util;
using Aion.Util.Services;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Services.Downtimes;

public class GetDowntimes
(
    ILogger<GetDowntimes> logger,
    GetProfile getProfile,
    FindWorkflows findWorkflows
)
{
    public async Task<ImmutableList<WorkflowDowntime>> Where(string profileName)
    {
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });

        var profile = getProfile.Single(profileName);
        var workflowDowntimes = new List<WorkflowDowntime>();

        foreach (var workflowPath in findWorkflows.Where(WorkflowSearchCriteria.Where(profile)))
        {
            try
            {
                if (await WorkflowDowntime.FromFile(workflowPath) is { } lockFile)
                {
                    workflowDowntimes.Add(lockFile);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to load lock file '{LockFileName}'.", workflowPath);
            }
        }

        return workflowDowntimes.ToImmutableList();
    }
}