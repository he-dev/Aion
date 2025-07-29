using System;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services;
using Aion.Core.Services.Scheduling;
using Aion.Util;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aion.Home.Endpoints.Workflows;

[ApiController]
[Route("api/profiles/{profileName}/workflows")]
public class StartsWorkflowOnPost
(
    ILogger<StartsWorkflowOnPost> logger,
    FindsWorkflows findsWorkflows,
    SchedulesWorkflowOnce schedulesWorkflowOnce
) : ControllerBase
{
    [HttpPost("{workflowName}:start-now")]
    public async Task<IActionResult> StartNow(string profileName, string workflowName)
    {
        return await Start(profileName, workflowName, async workflow => await schedulesWorkflowOnce.Now(profileName, workflow));
    }

    [HttpPost("{workflowName}:start-in")]
    public async Task<IActionResult> StartIn(string profileName, string workflowName, [FromBody] StartInBody body)
    {
        return await Start(profileName, workflowName, async workflow => await schedulesWorkflowOnce.In(profileName, workflow, body.Wait));
    }

    [HttpPost("{workflowName}:start-at")]
    public async Task<IActionResult> StartAt(string profileName, string workflowName, [FromBody] StartAtBody body)
    {
        return await Start(profileName, workflowName, async workflow => await schedulesWorkflowOnce.At(profileName, workflow, body.WhenUtc));
    }

    private async Task<IActionResult> Start(string profile, string workflowName, Func<Workflow, Task<DateTimeOffset>> action)
    {
        try
        {
            var fileName = findsWorkflows.Where(profile, workflowName).SingleOrThrows
            (
                onEmpty: () => new WorkflowNotFoundException(workflowName),
                onAmbiguous: () => new MultipleWorkflowsFoundException(workflowName)
            );
            var workflow = await Workflow.FromFile(fileName);
            var next = await action(workflow);
            return Accepted(new { next = next.ToLocalTime() });
        }
        catch (WorkflowNotFoundException)
        {
            return NotFound("No workflow matches the name '{$workflowName}'.");
        }
        catch (MultipleWorkflowsFoundException)
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