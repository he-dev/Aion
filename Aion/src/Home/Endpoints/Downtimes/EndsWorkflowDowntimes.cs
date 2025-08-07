using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services.Meta.Mvc;
using Aion.Meta.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Home.Endpoints.Downtimes;

[ApiController]
[Route("api/profiles/{profileName}/downtimes")]
public class EndsWorkflowDowntime
(
    ILogger<EndsWorkflowDowntime> logger,
    IOptionsSnapshot<EngineOptions> engineOptions
) : ControllerBase
{
    [HttpPost(":end")]
    [EnsuresProfileExists]
    public async Task<IActionResult> Where(string profileName, [FromBody] EndBody body)
    {
        var profile = engineOptions.Value[profileName];
        var workflowMatches = ImmutableList<WorkflowMatch>.Empty;

        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName });

        foreach (var workflowMatch in profile.Workflows.Where(body.Filter))
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