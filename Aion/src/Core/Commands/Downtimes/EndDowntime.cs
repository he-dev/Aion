using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Options;
using Aion.Core.Workflows;
using Aion.Util.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Core.Commands.Downtimes;

public class EndDowntime
(
    ILogger<EndDowntime> logger,
    IOptionsSnapshot<SchedulerOptions> schedulerOptions
)
{
    public async Task<object> Invoke(string profileName, string? filter = null)
    {
        var profile = schedulerOptions.Value.Profiles[profileName];
        var workflowMatches = ImmutableList<WorkflowMatch>.Empty;

        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });

        foreach (var workflowMatch in profile.Workflows.Find(filter))
        {
            try
            {
                if (await WorkflowDowntime.FromFile(workflowMatch.Path) is { } lockFile)
                {
                    await lockFile.EndsNow();
                    workflowMatches = workflowMatches.Add(workflowMatch);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to end downtime for '{WorkflowName}'.", workflowMatch.Name);
            }
        }

        return new
        {
            profile = profile.Path,
            workflows = workflowMatches.Select(m => m.PathWithinProfile),
        };
    }
}