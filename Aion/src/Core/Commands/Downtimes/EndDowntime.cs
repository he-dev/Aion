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
        var workflowPaths = ImmutableList<WorkflowPath>.Empty;

        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });

        foreach (var workflowPath in profile.Workflows.Find(filter))
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
            workflows = workflowPaths.Select(m => m.WorkflowName.RelativePath),
        };
    }
}