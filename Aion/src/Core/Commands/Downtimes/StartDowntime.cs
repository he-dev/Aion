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

public class StartDowntime
(
    ILogger<StartDowntime> logger,
    IOptionsSnapshot<SchedulerOptions> schedulerOptions
)
{
    public async Task<object> Invoke(string profileName, string workflowNameOrFilter, WorkflowDowntime downtime)
    {
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });
        try
        {
            var profile = schedulerOptions.Value.Profiles[profileName];
            var workflowMatches = ImmutableList<WorkflowPath>.Empty;
            foreach (var workflowMatch in profile.Workflows.Find(workflowNameOrFilter))
            {
                try
                {
                    var workflowLock = await downtime.ToFile(workflowMatch);
                    workflowMatches = workflowMatches.Add(workflowMatch);
                    logger.LogInformation("Workflow '{WorkflowName}' has been locked.", workflowMatch.WorkflowName);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unable to schedule maintenance for '{WorkflowName}'.", workflowMatch.WorkflowName);
                }
            }

            return new
            {
                profile = profile.Path,
                downtime = downtime,
                workflows = workflowMatches.Select(m => m.WorkflowName.RelativePath)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to schedule maintenance for '{WorkflowNameOrFilter}'.", workflowNameOrFilter);
            //return Problem(detail: ex.ToString(), statusCode: 500);
        }

        return null;
    }
}