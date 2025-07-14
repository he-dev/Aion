using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Util;
using Aion.Util.Quartz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aion.Home.Controllers;

[ApiController]
[Route("api")]
public class WorkflowsController
(
    ILogger<WorkflowsController> logger,
    WorkflowDirectory workflowDirectory,
    WorkflowScheduler workflowScheduler
) : ControllerBase
{
    [HttpGet("[controller]")]
    public async Task<IActionResult> Get([FromQuery(Name = "q")] string? filter, [FromQuery] bool? isOn = null)
    {
        // note: Uses Workflow as the type and not an object so that we can calculate next later and sort them.
        var workflows = ImmutableList<Workflow>.Empty;
        var errors = ImmutableList<object>.Empty;
        foreach (var filePath in workflowDirectory.FindFiles(filter ?? FileFilter.Any, FileExtension.Json))
        {
            try
            {
                workflows = workflows.Add(await Workflow.FromFile(filePath));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to load workflow from '{WorkflowPath}'.", filePath);
                errors = errors.Add(new { path = filePath, exception = ex.ToString() });
            }
        }

        var utcNow = DateTimeOffset.UtcNow; // note: Keeps the timestamp stable for all items.
        var result =
            from workflow in workflows
            let next = workflow.Trigger.FiresAt(utcNow).Take(3).Select(x => x.ToLocalTime())
            orderby next.FirstOrDefault(), workflow.Name
            select new
            {
                path = workflow.Path,
                isOn = workflow.IsOn,
                cron = workflow.Cron,
                next = next,
                jobs = workflow.Steps.Count(s => s.IsOn),
            };

        return Ok(new { result, errors });
    }

    // core: This API can synchronize workflows outside the regular synchronization schedule.
    // core: Does not use the synchronization-job here because we want to see the results immediately in the response.
    [HttpPost("[controller]:sync")]
    public async Task<IActionResult> Synchronize()
    {
        var result = ImmutableList<object>.Empty;
        var errors = ImmutableList<object>.Empty;

        foreach (var path in workflowDirectory.FindFiles(FileFilter.Any, FileExtension.Json))
        {
            try
            {
                if (await Workflow.FromFile(path) is { } workflow)
                {
                    var (sync, next) = await workflowScheduler.Synchronize(workflow);
                    result = result.Add(new
                    {
                        path,
                        sync,
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
            result,
            errors
        });
    }

    [HttpPost("[controller]/{name}:startNow")]
    public async Task<IActionResult> StartNow(string name)
    {
        return await Start(name, async workflow => await workflowScheduler.StartNow(workflow));
    }

    [HttpPost("[controller]/{name}:startIn")]
    public async Task<IActionResult> StartIn([FromRoute] string name, [FromBody] StartInBody body)
    {
        return await Start(name, async workflow => await workflowScheduler.StartIn(workflow, body.Wait));
    }

    [HttpPost("[controller]/{name}:startAt")]
    public async Task<IActionResult> StartAt([FromBody] StartAtBody body, [FromRoute] string name)
    {
        var startAt = body.When.FixMissingOffset().ToUniversalTime();
        return await Start(name, async workflow => await workflowScheduler.StartAt(workflow, startAt));
    }

    private async Task<IActionResult> Start(string name, Func<Workflow, Task<DateTimeOffset>> action)
    {
        try
        {
            var fileName = workflowDirectory.FindFile(name, FileExtension.Json);
            var workflow = await Workflow.FromFile(fileName);
            var next = await action(workflow);
            return Ok(new { name, next = next.ToLocalTime() });
        }
        catch (WorkflowNotFoundException)
        {
            return NotFound(new { name });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to schedule workflow '{WorkflowName}'.", name);
            return Problem(
                detail: ex.ToString(),
                title: $"Unable to schedule workflow '{name}'.",
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