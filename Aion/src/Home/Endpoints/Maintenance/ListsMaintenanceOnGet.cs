using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Flairs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aion.Home.Endpoints.Maintenance;

[ApiController]
[Route("api/profiles/{profileName}/maintenance")]
public class ListsMaintenanceOnGet
(
    ILogger<ListsMaintenanceOnGet> logger,
    FindsWorkflows findsWorkflows
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string profileName)
    {
        var lockFileNames = findsWorkflows.Where(profileName, FileFilter.Any);
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
                s.CreatedOnUtc,
                s.StartsOnUtc,
                s.EndsOnUtc,
                s.Duration,
                s.Remaining,
                s.IsPending,
                s.IsRunning,
                s.IsExpired,
            };

        return Ok(query.ToList());
    }
}