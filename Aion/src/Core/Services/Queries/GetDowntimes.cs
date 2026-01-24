using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Meta.Logging;
using Aion.Util;
using Aion.Util.Services;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Services.Queries;

public class GetDowntimes
(
    ILogger<GetDowntimes> logger,
    GetProfile getProfile,
    FindWorkflows findWorkflows
)
{
    public async Task<object> Invoke(string profileName)
    {
        var profile = getProfile.Single(profileName);
        var workflowDowntimes = ImmutableList<WorkflowDowntime>.Empty;

        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });

        foreach (var workflowPath in findWorkflows.Where(WorkflowSearchCriteria.Where(profile)))
        {
            try
            {
                if (await WorkflowDowntime.FromFile(workflowPath) is { } lockFile)
                {
                    workflowDowntimes = workflowDowntimes.Add(lockFile);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to load lock file '{LockFileName}'.", workflowPath);
            }
        }

        var downtimes =
            from s in workflowDowntimes
            orderby s.Remaining descending, s.Duration descending
            select new
            {
                s.FileName,
                CreatedOn = s.CreatedOnUtc.ToLocalTime(),
                StartsOn = s.StartsOnUtc.ToLocalTime(),
                EndsOn = s.EndsOnUtc.ToLocalTime(),
                s.Duration,
                s.Remaining,
                Status = s.Status.ToString()
            };

        return new
        {
            profile = profile.Path,
            downtimes
        };
    }
}