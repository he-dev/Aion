using System;
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
    public async Task<IImmutableList<WorkflowPath>> Now(string profileName, string pattern, WorkflowDowntime downtime)
    {
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });
        try
        {
            var profile = getProfile.Single(profileName);
            var workflowPaths = ImmutableList<WorkflowPath>.Empty;
            foreach (var workflowPath in findWorkflows.Where(WorkflowSearchCriteria.Where(profile, pattern)))
            {
                try
                {
                    var workflowLock = await downtime.ToFile(workflowPath);
                    workflowPaths = workflowPaths.Add(workflowPath);
                    logger.LogInformation("Workflow '{WorkflowName}' has been locked by '{WorkflowLock}'.", workflowPath.WorkflowName, workflowLock);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unable to schedule maintenance for '{WorkflowName}'.", workflowPath.WorkflowName);
                }
            }

            return workflowPaths;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to schedule maintenance for '{WorkflowNameOrFilter}'.", pattern);
            //return Problem(detail: ex.ToString(), statusCode: 500);
            throw;
        }
    }
}