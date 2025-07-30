using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aion.Home.Endpoints.Maintenance;

[ApiController]
[Route("api/profiles/{profileName}/maintenance")]
public class SchedulesMaintenanceOnPost
(
    ILogger<SchedulesMaintenanceOnPost> logger,
    FindsWorkflows findsWorkflows
) : ControllerBase
{
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
            throw new NoMatchException(profileName, workflowFilter);
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