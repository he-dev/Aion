using System;
using System.Threading.Tasks;
using Aion.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Home.Endpoints.Workflows;

[ApiController]
[Route("api/profiles/{profileName}/workflows")]
public class SchedulesWorkflowOnce
(
    ILogger<SchedulesWorkflowOnce> logger,
    IOptions<InstanceOptions> engineOptions,
    Core.Services.Scheduling.SchedulesWorkflowOnce schedulesWorkflowOnce
) : ControllerBase
{
    [HttpPost("{workflowName}:start-now")]
    public async Task<IActionResult> ToStartNow(string profileName, string workflowName)
    {
        return await Start(profileName, workflowName, async workflowMatch => await schedulesWorkflowOnce.ToStartNow(workflowMatch));
    }

    [HttpPost("{workflowName}:start-in")]
    public async Task<IActionResult> ToStartIn(string profileName, string workflowName, [FromBody] StartInBody body)
    {
        return await Start(profileName, workflowName, async workflowMatch => await schedulesWorkflowOnce.ToStartIn(workflowMatch, body.Wait));
    }

    [HttpPost("{workflowName}:start-at")]
    public async Task<IActionResult> ToStartAt(string profileName, string workflowName, [FromBody] StartAtBody body)
    {
        return await Start(profileName, workflowName, async workflowMatch => await schedulesWorkflowOnce.ToStartAt(workflowMatch, body.WhenUtc));
    }

    private async Task<IActionResult> Start(string profileName, string workflowName, Func<WorkflowMatch, Task<DateTimeOffset>> action)
    {
        try
        {
            var profile = engineOptions.Value[profileName];
            var workflowMatch = profile.Workflows.Single(workflowName);
            var next = await action(workflowMatch);
            return Accepted(new { next = next.ToLocalTime() });
        }
        catch (NoWorkflowMatch)
        {
            return NotFound("No workflow matches the name '{$workflowName}'.");
        }
        catch (AmbiguousWorkflowMatch)
        {
            return BadRequest("Multiple workflows match the name '{$workflowName}'.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to schedule workflow '{WorkflowName}'.", workflowName);
            return Problem(
                detail: ex.ToString(),
                title: $"Unable to schedule workflow '{workflowName}'.",
                statusCode: 500,
                instance: Request.Path
            );
        }
    }

    public record StartInBody
    {
        public TimeSpan Wait { get; init; }
    }

    // note: Does not validate the input because the scheduler does that already.
    public record StartAtBody
    {
        public DateTime When { get; init; }

        public DateTimeOffset WhenUtc => When.ToUniversalTime();
    }
}