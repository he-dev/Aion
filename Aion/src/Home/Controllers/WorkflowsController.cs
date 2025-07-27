using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Flairs;
using Aion.Core.Flairs.Scheduling;
using Aion.Util;
using Aion.Util.Quartz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aion.Home.Controllers;

[ApiController]
[Route("api/profiles/{profileName}/[controller]")]
public class WorkflowsController
(
    ILogger<WorkflowsController> logger,
    IOptions<EngineOptions> engineOptions,
    FindsWorkflows findsWorkflows,
    SchedulesWorkflowOnce schedulesWorkflowOnce,
    SynchronizesWorkflowCron synchronizesWorkflowCron
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
            profile = engineOptions.Value[profileName].Path,
            result,
            errors
        });
    }

    // core: Synchronizes workflows outside the regular synchronization schedule.
    [HttpPost(":sync")]
    public async Task<IActionResult> Synchronize(string profileName)
    {
        var result = ImmutableList<object>.Empty;
        var errors = ImmutableList<object>.Empty;

        foreach (var path in findsWorkflows.Where(profileName, FileFilter.Any))
        {
            try
            {
                if (await Workflow.FromFile(path) is { } workflow)
                {
                    // core: Not using the synchronization-job because we want to see the results immediately in the response.
                    var (sync, deleted, next) = await synchronizesWorkflowCron.For(profileName, workflow);
                    result = result.Add(new
                    {
                        path,
                        sync,
                        deleted,
                        next = next?.ToLocalTime(),
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to synchronize workflow '{WorkflowPath}'.", path);
                errors = errors.Add(new
                {
                    path,
                    exception = ex.ToString()
                });
            }
        }

        return Ok(new
        {
            profile = engineOptions.Value[profileName].Path,
            result,
            errors
        });
    }

    [HttpPost("{workflowName}:start-now")]
    public async Task<IActionResult> StartNow(string profileName, string workflowName)
    {
        return await Start(profileName, workflowName, async workflow => await schedulesWorkflowOnce.Now(workflow));
    }

    [HttpPost("{workflowName}:start-in")]
    public async Task<IActionResult> StartIn(string profileName, string workflowName, [FromBody] StartInBody body)
    {
        return await Start(profileName, workflowName, async workflow => await schedulesWorkflowOnce.In(workflow, body.Wait));
    }

    [HttpPost("{workflowName}:start-at")]
    public async Task<IActionResult> StartAt(string profileName, string workflowName, [FromBody] StartAtBody body)
    {
        var startAt = body.When.UseTimeZoneOffsetOrLocal().ToUniversalTime();
        return await Start(profileName, workflowName, async workflow => await schedulesWorkflowOnce.At(workflow, startAt));
    }

    private async Task<IActionResult> Start(string profile, string workflowName, Func<Workflow, Task<DateTimeOffset>> action)
    {
        try
        {
            var fileName = findsWorkflows.Where(profile, workflowName).SingleOrThrows();
            var workflow = await Workflow.FromFile(fileName);
            var next = await action(workflow);
            return Ok(new { workflowFilter = workflowName, next = next.ToLocalTime() });
        }
        catch (CollectionEmptyException ex)
        {
            return NotFound(new { workflowFilter = workflowName }); // todo: say why
        }
        catch (AmbiguousResultException ex)
        {
            return NotFound(new { workflowFilter = workflowName }); // todo: say why
        }
        catch (WorkflowNotFoundException)
        {
            return NotFound(new { workflowFilter = workflowName });
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
        public DateTimeOffset When { get; init; }
    }
}