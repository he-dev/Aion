using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Mvc;
using Aion.Core.Options;
using Aion.Core.Workflows;
using Aion.Util.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Home.Controllers;

[ApiController]
[Route("api/profiles/{profileName}/downtimes")]
public class DowntimesController
(
    ILogger<DowntimesController> logger,
    IOptionsSnapshot<InstanceOptions> engineOptions
) : ControllerBase
{
    [HttpGet]
    [ProfileExistenceValidation]
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
                };

        return Ok(new
        {
            profile = profile.Path,
            downtimes
        });
    }

    [HttpPost(":start-in")]
    [ProfileExistenceValidation]
    public async Task<IActionResult> ToStartIn(string profileName, [FromBody] StartInBody body)
    {
        return await Starts(profileName, body.Filter, () => WorkflowDowntime.StartsIn(body.Wait, body.Duration));
    }

    [HttpPost(":start-at")]
    [ProfileExistenceValidation]
    public async Task<IActionResult> ToStartAt(string profileName, [FromBody] StartAtBody body)
    {
        return await Starts(profileName, body.Filter, () => WorkflowDowntime.StartsAt(body.StartsAtUtc, body.EndsAtUtc));
    }

    private async Task<IActionResult> Starts(string profileName, string workflowNameOrFilter, Func<WorkflowDowntime> createsWorkflowDowntime)
    {
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });
        try
        {
            var profile = engineOptions.Value[profileName];
            var workflowDowntime = createsWorkflowDowntime();
            var workflowMatches = ImmutableList<WorkflowMatch>.Empty;
            foreach (var workflowMatch in profile.Workflows.Find(workflowNameOrFilter))
            {
                try
                {
                    var workflowLock = await workflowDowntime.ToFile(workflowMatch.Path);
                    workflowMatches = workflowMatches.Add(workflowMatch);
                    logger.LogInformation("Workflow '{WorkflowName}' has been locked.", workflowMatch.Name);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unable to schedule maintenance for '{WorkflowName}'.", workflowMatch.Name);
                }
            }

            return Ok(new
            {
                profile = profile.Path,
                downtime = workflowDowntime,
                workflows = workflowMatches.Select(m => m.PathWithinProfile)
            });
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

    [HttpPost(":end")]
    [ProfileExistenceValidation]
    public async Task<IActionResult> Where(string profileName, [FromBody] EndBody body)
    {
        var profile = engineOptions.Value[profileName];
        var workflowMatches = ImmutableList<WorkflowMatch>.Empty;

        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });

        foreach (var workflowMatch in profile.Workflows.Find(body.Filter))
        {
            try
            {
                if (await WorkflowDowntime.FromFile(workflowMatch.Path) is { } lockFile)
                {
                    await lockFile.EndsNow();
                    workflowMatches = workflowMatches.Add(workflowMatch);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to end downtime for '{WorkflowName}'.", workflowMatch.Name);
            }
        }

        return Ok(new
        {
            profile = profile.Path,
            workflows = workflowMatches.Select(m => m.PathWithinProfile),
        });
    }

    public record EndBody
    {
        public string Filter { get; init; } = null!;
    }
}