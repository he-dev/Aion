using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services.Meta.Mvc;
using Aion.Meta.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Home.Endpoints.Downtimes;

[ApiController]
[Route("api/profiles/{profileName}/downtimes")]
public class ListsWorkflowDowntimes
(
    ILogger<ListsWorkflowDowntimes> logger,
    IOptionsSnapshot<EngineOptions> engineOptions
) : ControllerBase
{
    [HttpGet]
    [EnsuresProfileExists]
    public async Task<IActionResult> Get(string profileName)
    {
        var profile = engineOptions.Value[profileName];
        var workflowDowntimes = ImmutableList<WorkflowDowntime>.Empty;

        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });

        foreach (var workflowMatch in profile.Workflows.All())
        {
            try
            {
                if (await WorkflowDowntime.FromFile(workflowMatch.Path) is { } lockFile)
                {
                    workflowDowntimes = workflowDowntimes.Add(lockFile);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to load lock file '{LockFileName}'.", workflowMatch.Path);
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
                }
            ;

        return Ok(new
        {
            profile = profile.Path,
            downtimes
        });
    }
}