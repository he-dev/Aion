using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Modules;
using Aion.Modules.Services.Queries;
using Aion.Toolbox.Logging;
using Microsoft.Extensions.Logging;

namespace Aion.Context.Services.Commands;

public class EndDowntime
(
    ILogger<EndDowntime> logger,
    GetProfile getProfile
)
{
    public async Task<object> Now(string profileName, string? filter = null)
    {
        var profile = getProfile.Where(profileName);
        var workflowPaths = ImmutableList<WorkflowPath>.Empty;

        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });

        foreach (var workflowPath in profile.Workflows.Where(filter))
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