using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services;
using Aion.Core.Services.Scheduling;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Home.Endpoints.Workflows;

[ApiController]
[Route("api/profiles/{profileName}/workflows")]
public class SynchronizesWorkflowsOnPost
(
    ILogger<SynchronizesWorkflowsOnPost> logger,
    IOptions<EngineOptions> engineOptions,
    FindsWorkflows findsWorkflows,
    SynchronizesWorkflowCron synchronizesWorkflowCron
) : ControllerBase
{
    // core: Synchronizes workflows outside the regular synchronization schedule.
    [HttpPost(":sync")]
    public async Task<IActionResult> Synchronize(string profileName)
    {
        var result = ImmutableList<object>.Empty;
        var errors = ImmutableList<object>.Empty;

        foreach (var path in findsWorkflows.Where(profileName))
        {
            try
            {
                if (await Workflow.FromFile(path) is { } workflow)
                {
                    // core: Not using the synchronization-job because we want to see the results immediately in the response.
                    var (sync, deleted, next) = await synchronizesWorkflowCron.For(profileName, workflow);
                    result = result.Add(new
                    {
                        path,
                        sync = sync.ToString(),
                        deleted,
                        next = next?.ToLocalTime(),
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to synchronize workflow '{WorkflowPath}'.", path);
                errors = errors.Add(new
                {
                    path,
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