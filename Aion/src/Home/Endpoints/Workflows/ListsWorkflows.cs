using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services.Meta.Mvc;
using Aion.Util.Quartz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Home.Endpoints.Workflows;

[ApiController]
[Route("api/profiles/{profileName}/workflows")]
public class ListsWorkflows
(
    ILogger<ListsWorkflows> logger,
    IOptionsSnapshot<EngineOptions> engineOptions
) : ControllerBase
{
    [HttpGet]
    [EnsuresProfileExists]
    public async Task<IActionResult> Get(string profileName, [FromQuery(Name = "q")] string? workflowFilter, [FromQuery] bool? isOn = null)
    {
        var profile = engineOptions.Value[profileName];

        // note: Uses Workflow as the type and not an object so that we can calculate next later and sort them.
        var workflowMatches = ImmutableList<WorkflowMatch>.Empty;
        var workflowFailure = ImmutableList<object>.Empty;
        var matchesWorkflows = workflowFilter is not null ? profile.Workflows.Where(workflowFilter) : profile.Workflows.All();
        foreach (var workflowMatch in matchesWorkflows)
        {
            try
            {
                if (workflowMatch.Name.IsUrlSafe)
                {
                    workflowMatches = workflowMatches.Add(await workflowMatch.Load());
                    logger.LogDebug("Successfully loaded workflow from '{WorkflowPath}'.", workflowMatch.Path);
                }
                else
                {
                    workflowFailure = workflowFailure.Add(new { path = workflowMatch.PathWithinProfile, issue = "Workflow name is not url-safe." });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to load workflow from '{WorkflowPath}'.", workflowMatch.Path);
                workflowFailure = workflowFailure.Add(new { path = workflowMatch.PathWithinProfile, issue = ex.ToString() });
            }
        }

        var utcNow = DateTimeOffset.UtcNow; // note: Keeps the timestamp stable for all items.
        var result =
            from match in workflowMatches
            let next = match.CronTrigger.FiresAt(utcNow).Take(3).Select(x => x.ToLocalTime())
            //orderby next.FirstOrDefault(), match.Name
            orderby match.Name.ToString()
            select new
            {
                path = match.Path,
                name = match.Name.ToString(),
                isOn = match.Value.IsOn,
                cron = match.Value.Cron,
                next = next,
                jobs = match.Value.Steps.Count(s => s.IsOn),
            };

        return Ok(new
        {
            profile = new
            {
                engineOptions.Value[profileName].Name,
                engineOptions.Value[profileName].Path,
            },
            result,
            issues = workflowFailure
        });
    }
}