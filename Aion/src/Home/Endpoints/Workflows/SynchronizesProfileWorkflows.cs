using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services.Scheduling;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Home.Endpoints.Workflows;

[ApiController]
[Route("api/profiles/{profileName}/workflows")]
public class SynchronizesProfileWorkflows
(
    ILogger<SynchronizesProfileWorkflows> logger,
    IOptions<EngineOptions> engineOptions,
    SchedulesWorkflowCron schedulesWorkflowCron
) : ControllerBase
{
    // core: Synchronizes workflows outside the regular synchronization schedule.
    [HttpPost(":sync")]
    public async Task<IActionResult> Synchronize(string profileName)
    {
        var profile = engineOptions.Value[profileName];
        var result = ImmutableList<object>.Empty;
        var errors = ImmutableList<object>.Empty;


        var workflowMatches = profile.Workflows.All();
        foreach (var workflowMatch in workflowMatches)
        {
            try
            {
                await workflowMatch.Load();

                // core: Not using the synchronization-job because we want to see the results immediately in the response.
                var (sync, deleted, next) = await schedulesWorkflowCron.For(workflowMatch);
                result = result.Add(new
                {
                    path = workflowMatch.Path,
                    sync = sync.ToString(),
                    deleted,
                    next = next?.ToLocalTime(),
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to synchronize workflow '{WorkflowPath}'.", workflowMatch.Path);
                errors = errors.Add(new
                {
                    path = workflowMatch.Path,
                    exception = ex.ToString()
                });
            }
        }

        return Ok(new
        {
            profile = new
            {
                engineOptions.Value[profileName].Name,
                engineOptions.Value[profileName].Path,
            },
            result,
            errors
        });
    }
}