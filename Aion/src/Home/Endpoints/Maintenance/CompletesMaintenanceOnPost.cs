using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aion.Home.Endpoints.Maintenance;

[ApiController]
[Route("api/profiles/{profileName}/maintenance")]
public class CompletesMaintenanceOnPost
(
    ILogger<CompletesMaintenanceOnPost> logger,
    FindsWorkflows findsWorkflows
) : ControllerBase
{
    [HttpPost(":complete")]
    public async Task<IActionResult> DeleteWhere(string profileName, [FromBody] CompleteBody body)
    {
        var lockFileNames = findsWorkflows.Where(profileName, body.Filter);
        var locks = ImmutableList<MaintenancePeriod>.Empty;
        foreach (var lockFileName in lockFileNames)
        {
            try
            {
                if (await MaintenancePeriod.FromFile(lockFileName) is { } lockFile)
                {
                    locks = locks.Add(lockFile);
                }
            }
            catch (FileNotFoundException)
            {
                // core: Ignore this error as it is by design.
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to load lock file '{LockFileName}'.", lockFileName);
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
        public string Filter { get; init; } = null!;
    }
}