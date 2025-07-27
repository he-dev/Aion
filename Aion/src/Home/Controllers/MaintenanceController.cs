using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Flairs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aion.Home.Controllers;

[ApiController]
[Route("api/profiles/{profileName}/[controller]")]
public class MaintenanceController
(
    ILogger<MaintenanceController> logger,
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

    [HttpPost(":start-in")]
    public async Task<IActionResult> StartIn(string profileName, [FromBody] StartInBody body)
    {
        try
        {
            var maintenancePeriod = MaintenancePeriod.StartsIn(body.Wait, body.Duration);
            var lockedWorkflows = await Apply(profileName, body.Filter, maintenancePeriod).ToListAsync();
            return Ok(new { lockedWorkflows });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to schedule maintenance for '{filter}'.", body.Filter);
            return Problem(detail: ex.ToString(), statusCode: 500);
        }
    }

    [HttpPost(":start-at")]
    public async Task<IActionResult> StartAt(string profileName, [FromBody] StartAtBody body)
    {
        try
        {
            var maintenancePeriod = MaintenancePeriod.StartsAt(body.StartsOnUtc, body.EndsOnUtc);
            var lockedWorkflows = await Apply(profileName, body.Filter, maintenancePeriod).ToListAsync();
            return Ok(new { lockedWorkflows });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to schedule maintenance for '{filter}'.", body.Filter);
            return Problem(detail: ex.ToString(), statusCode: 500);
        }
    }

    private async IAsyncEnumerable<string> Apply(string profileName, string workflowFilter, MaintenancePeriod maintenancePeriod)
    {
        var lockCount = 0;
        foreach (var workflowFile in findsWorkflows.Where(profileName, workflowFilter))
        {
            var workflowLockPath = await maintenancePeriod.ToFile(workflowFile);
            logger.LogInformation("Workflow '{WorkflowFile}' has been locked.", workflowFile);
            yield return workflowLockPath;
            lockCount++;
        }

        if (lockCount == 0)
        {
            throw new WorkflowNotFoundException(workflowFilter);
        }
    }

    public abstract record StartBody
    {
        public string Filter { get; init; } = null!;
    }

    public record StartInBody : StartBody
    {
        public TimeSpan Wait { get; init; }

        public TimeSpan Duration { get; init; }
    }

    public record StartAtBody : StartBody
    {
        public DateTime StartsOn { get; init; }

        public DateTimeOffset StartsOnUtc => StartsOn.ToUniversalTime();

        public DateTime EndsOn { get; init; }

        public DateTimeOffset EndsOnUtc => EndsOn.ToUniversalTime();
    }
}