using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Aion.Core.Entities;
using Aion.Core.Services;
using Aion.Core.Services.Mvc;
using Aion.Util.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Home.Controllers;

[ApiController]
[Route("api/profiles/{profileName}/workflows")]
public class WorkflowsController
(
    ILogger<WorkflowsController> logger,
    IOptionsSnapshot<InstanceOptions> engineOptions,
    WorkflowScheduleRegistry workflowScheduleRegistry
) : ControllerBase
{
    [HttpGet]
    [ProfileExistenceValidation]
    public async Task<IActionResult> Get(string profileName, [FromQuery(Name = "q")] string? workflowFilter, [FromQuery] bool? isOn = null)
    {
        var profile = engineOptions.Value[profileName];

        // note: Uses Workflow as the type and not an object so that we can calculate next later and sort them.
        var workflows = ImmutableList<Workflow>.Empty;
        var workflowFailure = ImmutableList<object>.Empty;
        var matchesWorkflows = workflowFilter is not null ? profile.Workflows.Find(workflowFilter) : profile.Workflows.All();
        foreach (var workflowMatch in matchesWorkflows)
        {
            try
            {
                if (workflowMatch.Name.IsUrlSafe)
                {
                    //workflows = workflows.Add(await workflowMatch.Load());
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
        // var result =
        //     from match in workflows
        //     let next = match.CronTrigger.FiresAt(utcNow).Take(3).Select(x => x.ToLocalTime())
        //     //orderby next.FirstOrDefault(), match.Name
        //     orderby match.Name.ToString()
        //     select new
        //     {
        //         path = match.Path,
        //         name = match.Name.ToString(),
        //         isOn = match.Template.Enabled,
        //         cron = match.Template.Cron,
        //         next = next,
        //         jobs = match.Template.Steps.Count(s => s.Enabled),
        //     };

        var result = string.Empty;

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

    [HttpPost("{workflowName}:start-now")]
    public async Task<IActionResult> ToStartNow(string profileName, string workflowName)
    {
        return await Start(profileName, workflowName, null);
    }

    [HttpPost("{workflowName}:start-in")]
    public async Task<IActionResult> ToStartIn(string profileName, string workflowName, [FromBody] StartInBody body)
    {
        return await Start(profileName, workflowName, DateTimeOffset.UtcNow + body.Wait);
    }

    [HttpPost("{workflowName}:start-at")]
    public async Task<IActionResult> ToStartAt(string profileName, string workflowName, [FromBody] StartAtBody body)
    {
        return await Start(profileName, workflowName, body.WhenUtc);
    }

    private async Task<IActionResult> Start(string profileName, string workflowName, DateTimeOffset? startAtUtc)
    {
        try
        {
            var profile = engineOptions.Value[profileName];
            var workflowMatch = profile.Workflows.Single(workflowName);
            var workflow = await workflowMatch.ToWorkflow(ImmutableList<TemplateVariableGroup>.Empty);
            var result = await workflowScheduleRegistry.AddOrUpdate(workflow, workflow.OnceTrigger(startAtUtc));
            return Accepted(new { next = result.NextUtc!.Value.ToLocalTime() });
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

    // core: Synchronizes workflows outside the regular synchronization schedule.
    [HttpPost(":sync")]
    public async Task<IActionResult> Synchronize(string profileName)
    {
        var profile = engineOptions.Value[profileName];
        var result = ImmutableList<object>.Empty;
        var errors = ImmutableList<object>.Empty;

        var workflowMatches = profile.Workflows.All();
        foreach (var workflowMatch in workflowMatches)
        {
            try
            {
                var workflow = await workflowMatch.ToWorkflow(ImmutableList<TemplateVariableGroup>.Empty);

                // core: Not using the synchronization-job because we want to see the results immediately in the response.
                var (sync, deleted, next) = await workflowScheduleRegistry.AddOrUpdate(workflow);
                result = result.Add(new
                {
                    path = workflowMatch.Path,
                    sync = sync.ToString(),
                    deleted,
                    next = next?.ToLocalTime(),
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to synchronize workflow '{WorkflowPath}'.", workflowMatch.Path);
                errors = errors.Add(new
                {
                    path = workflowMatch.Path,
                    exception = ex.ToString()
                });
            }
        }

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