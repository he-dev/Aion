using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Flairs;
using Aion.Util.Quartz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Home.Endpoints.Workflows;

[ApiController]
[Route("api/profiles/{profileName}/workflows")]
public class ListsWorkflowsOnGet
(
    ILogger<ListsWorkflowsOnGet> logger,
    IOptions<EngineOptions> engineOptions,
    FindsWorkflows findsWorkflows
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string profileName, [FromQuery(Name = "q")] string? filter, [FromQuery] bool? isOn = null)
    {
        // note: Uses Workflow as the type and not an object so that we can calculate next later and sort them.
        var workflows = ImmutableList<Workflow>.Empty;
        var errors = ImmutableList<object>.Empty;
        foreach (var workflowPath in findsWorkflows.Where(profileName, filter ?? FileFilter.Any))
        {
            try
            {
                workflows = workflows.Add(await Workflow.FromFile(workflowPath));
                logger.LogDebug("Successfully loaded workflow from '{WorkflowPath}'.", workflowPath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to load workflow from '{WorkflowPath}'.", workflowPath);
                errors = errors.Add(new { path = workflowPath.ToString(), exception = ex.ToString() });
            }
        }

        var utcNow = DateTimeOffset.UtcNow; // note: Keeps the timestamp stable for all items.
        var result =
            from workflow in workflows
            let next = workflow.CreatesCronTrigger(profileName).FiresAt(utcNow).Take(3).Select(x => x.ToLocalTime())
            orderby next.FirstOrDefault(), workflow.Name
            select new
            {
                path = workflow.Path,
                isOn = workflow.IsOn,
                cron = workflow.Cron,
                next = next,
                jobs = workflow.Steps.Count(s => s.IsOn),
            };

        return Ok(new
        {
            profile = new
            {
                engineOptions.Value[profileName].Name,
                engineOptions.Value[profileName].Path,
            },
            result,
            errors
        });
    }
}