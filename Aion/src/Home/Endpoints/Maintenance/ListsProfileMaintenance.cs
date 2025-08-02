using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Util.Mvc;
using Aion.Util.Serilog;
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
        foreach (var workflowMatch in profile.Workflows())
        {
            try
            {
                if (await MaintenancePeriod.FromFile(workflowMatch.Path) is var lockFile)
                {
                    locks = locks.Add(lockFile);
                }
            }
            catch (FileNotFoundException)
            {
                // core: Ignore this error as it means that there is no workflow-lock in place.
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