using System;
using System.Collections.Immutable;
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
public class SchedulesProfileMaintenance
(
    ILogger<SchedulesProfileMaintenance> logger,
    IOptionsSnapshot<EngineOptions> engineOptions
) : ControllerBase
{
    [HttpPost(":start-in")]
    [EnsuresProfileExists]
    public async Task<IActionResult> ToStartIn(string profileName, [FromBody] StartInBody body)
    {
        return await Start(profileName, body.Filter, () => TestsMaintenancePeriod.StartsIn(body.Wait, body.Duration));
    }

    [HttpPost(":start-at")]
    [EnsuresProfileExists]
    public async Task<IActionResult> ToStartAt(string profileName, [FromBody] StartAtBody body)
    {
        return await Start(profileName, body.Filter, () => TestsMaintenancePeriod.StartsAt(body.StartsAtUtc, body.EndsAtUtc));
    }

    private async Task<IActionResult> Start(string profileName, string workflowNameOrFilter, Func<TestsMaintenancePeriod> createsMaintenancePeriod)
    {
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });
        try
        {
            var profile = engineOptions.Value[profileName];
            var maintenancePeriod = createsMaintenancePeriod();
            var lockedWorkflows = ImmutableList<WorkflowMatch>.Empty;
            foreach (var workflowMatch in profile.Workflows.Where(workflowNameOrFilter))
            {
                try
                {
                    lockedWorkflows = lockedWorkflows.Add(workflowMatch);
                    await maintenancePeriod.ToFile(workflowMatch.Path);
                    logger.LogInformation("Workflow '{WorkflowName}' has been locked.", workflowMatch.Name);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unable to schedule maintenance for '{WorkflowName}'.", workflowMatch.Name);
                }
            }

            return Ok(new { lockedWorkflows });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to schedule maintenance for '{WorkflowNameOrFilter}'.", workflowNameOrFilter);
            return Problem(detail: ex.ToString(), statusCode: 500);
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

        public DateTime StartsAt { get; init; }
        public DateTime EndsAt { get; init; }

        public DateTimeOffset StartsAtUtc => StartsAt.ToUniversalTime();
        public DateTimeOffset EndsAtUtc => EndsAt.ToUniversalTime();
    }
}