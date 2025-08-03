using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services.Meta.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Home.Endpoints.Maintenance;

[ApiController]
[Route("api/profiles/{profileName}/maintenance")]
public class CompletesProfileMaintenance
(
    ILogger<CompletesProfileMaintenance> logger,
    IOptionsSnapshot<EngineOptions> engineOptions
) : ControllerBase
{
    [HttpPost(":complete")]
    [EnsuresProfileExists]
    public async Task<IActionResult> Where(string profileName, [FromBody] CompleteBody body)
    {
        var profile = engineOptions.Value[profileName];
        var locks = ImmutableList<TestsMaintenancePeriod>.Empty;
        foreach (var workflowMatch in profile.Workflows.Where(body.WorkflowFilter))
        {
            try
            {
                if (await TestsMaintenancePeriod.FromFile(workflowMatch.Path) is { } lockFile)
                {
                    await lockFile.Complete();
                    locks = locks.Add(lockFile);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to load lock file '{LockFileName}'.", workflowMatch.Path);
            }
        }

        var query =
            from s in locks
            orderby s.Remaining descending
            select new
            {
                s.FileName,
                CreatedOn = s.CreatedOnUtc.ToLocalTime(),
                StartsOn = s.StartsOnUtc.ToLocalTime(),
                EndsOn = s.EndsOnUtc.ToLocalTime(),
                s.Duration,
                s.Remaining,
                s.Status
            };

        return Ok(query.ToList());
    }

    public record CompleteBody
    {
        public string WorkflowFilter { get; init; } = null!;
    }
}