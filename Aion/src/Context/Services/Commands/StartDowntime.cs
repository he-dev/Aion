using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Modules;
using Aion.Modules.Services.Queries;
using Aion.Toolbox.Logging;
using Microsoft.Extensions.Logging;

namespace Aion.Context.Services.Commands;

public class StartDowntime
(
    ILogger<StartDowntime> logger,
    GetProfile getProfile
)
{
    public async Task<IImmutableList<WorkflowPath>> Now(string profileName, string workflowNameOrFilter, WorkflowDowntime downtime)
    {
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });
        try
        {
            var profile = getProfile.Where(profileName);
            var workflowPaths = ImmutableList<WorkflowPath>.Empty;
            foreach (var workflowPath in profile.Workflows.Where(workflowNameOrFilter))
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
            logger.LogError(ex, "Unable to schedule maintenance for '{WorkflowNameOrFilter}'.", workflowNameOrFilter);
            //return Problem(detail: ex.ToString(), statusCode: 500);
            throw;
        }
    }
}