using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Aion.Meta.Logging;
using Aion.Util;
using Aion.Util.Services;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Services.Downtimes;

public class StartDowntime
(
    ILogger<StartDowntime> logger,
    GetProfile getProfile,
    FindWorkflows findWorkflows
)
{
    public async Task<IImmutableList<StartDowntimeResult>> Now(string profileName, string pattern, WorkflowDowntime downtime)
    {
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });
        try
        {
            var profile = getProfile.Single(profileName);
            var workflowPaths = new List<StartDowntimeResult>();
            foreach (var workflowPath in findWorkflows.Where(WorkflowSearchCriteria.Where(profile, pattern)))
            {
                try
                {
                    await downtime.ToFile(workflowPath);
                    workflowPaths.Add(new StartDowntimeResult(workflowPath.WorkflowName));
                    logger.LogInformation("Workflow '{WorkflowName}' has been locked for {Duration} starting at {StartsOnUtc} (UTC).", workflowPath.WorkflowName, downtime.Duration, downtime.StartsOnUtc);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unable to lock '{WorkflowName}'.", workflowPath.WorkflowName);
                    workflowPaths.Add(new StartDowntimeResult(workflowPath.WorkflowName) { Exception = ex });
                }
            }

            return workflowPaths.ToImmutableList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to lock '{WorkflowNameOrFilter}'.", pattern);
            //return Problem(detail: ex.ToString(), statusCode: 500);
            throw;
        }
    }
}

public record StartDowntimeResult(WorkflowName WorkflowName)
{
    public Exception? Exception { get; init; }
}