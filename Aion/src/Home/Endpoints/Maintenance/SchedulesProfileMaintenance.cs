using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Util.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Home.Endpoints.Maintenance;

[ApiController]
[Route("api/profiles/{profileName}/maintenance")]
public class SchedulesProfileMaintenance
(
    ILogger<SchedulesProfileMaintenance> logger,
    IOptionsSnapshot<EngineOptions> engineOptions
) : ControllerBase
{
    [HttpPost(":to-start-in")]
    [EnsuresProfileExists]
    public async Task<IActionResult> ToStartIn(string profileName, [FromBody] StartInBody body)
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

    [HttpPost(":to-start-at")]
    [EnsuresProfileExists]
    public async Task<IActionResult> ToStartAt(string profileName, [FromBody] StartAtBody body)
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
        var profile = engineOptions.Value[profileName];
        var lockCount = 0;
        foreach (var workflowMatch in profile.Workflows(workflowFilter))
        {
            var workflowLockPath = await maintenancePeriod.ToFile(workflowMatch.Path);
            logger.LogInformation("Workflow '{WorkflowName}' has been locked.", workflowMatch.Name);
            yield return workflowLockPath;
            lockCount++;
        }

        if (lockCount == 0)
        {
            throw new NoMatchException(profileName, workflowFilter);
        }
    }

    public record StartInBody
    {
        public string Filter { get; init; } = null!;

        public TimeSpan Wait { get; init; }

        public TimeSpan Duration { get; init; }
    }

    public record StartAtBody
    {
        public string Filter { get; init; } = null!;

        public DateTime StartsOn { get; init; }

        public DateTimeOffset StartsOnUtc => StartsOn.ToUniversalTime();

        public DateTime EndsOn { get; init; }

        public DateTimeOffset EndsOnUtc => EndsOn.ToUniversalTime();
    }
}