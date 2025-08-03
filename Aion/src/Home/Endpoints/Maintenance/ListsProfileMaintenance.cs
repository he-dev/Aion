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

namespace Aion.Home.Endpoints.Maintenance;

[ApiController]
[Route("api/profiles/{profileName}/maintenance")]
public class ListsProfileMaintenance
(
    ILogger<ListsProfileMaintenance> logger,
    IOptionsSnapshot<EngineOptions> engineOptions
) : ControllerBase
{
    [HttpGet]
    [EnsuresProfileExists]
    public async Task<IActionResult> Get(string profileName)
    {
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });

        var profile = engineOptions.Value[profileName];
        var locks = ImmutableList<MaintenancePeriod>.Empty;
        foreach (var workflowMatch in profile.Workflows.All())
        {
            try
            {
                if (await MaintenancePeriod.FromFile(workflowMatch.Path) is { } lockFile)
                {
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
                orderby s.Remaining descending, s.Duration descending
                select new
                {
                    s.FileName,
                    s.CreatedOnUtc,
                    s.StartsOnUtc,
                    s.EndsOnUtc,
                    s.Duration,
                    s.Remaining,
                    s.Status
                }
            ;

        return Ok(query.ToList());
    }
}