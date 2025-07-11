using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Util;
using Aion.Util.Quartz;
using Aion.Util.Serilog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aion.Home.Controllers;

[ApiController]
[Route("api")]
public class WorkflowsController
(
    ILogger<WorkflowsController> logger
) : ControllerBase
{
    [HttpGet("[controller]")]
    public async Task<IActionResult> Get
    (
        [FromServices] WorkflowDirectory workflowDirectory,
        [FromQuery(Name = "q")] string? filter,
        [FromQuery] bool? enabled = null
    )
    {
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

        var utcNow = DateTimeOffset.UtcNow; // clue: Keeps the timestamp stable for all items.
        var result =
            from workflow in workflows
            let next = workflow.Trigger.FiresAt(utcNow).Take(3)
            orderby next.FirstOrDefault().Value, workflow.Name
            select new
            {
                path = workflow.Path,
                isOn = workflow.Enabled,
                cron = workflow.Cron,
                next = next,
                cmds = workflow.Steps.Count(s => s.Enabled),
            };

        return Ok(new { result, errors });
    }

    // role: The API can synchronize workflows outside its regular schedule.
    [HttpPost("[controller]:sync")]
    public async Task<IActionResult> Synchronize
    (
        [FromServices] WorkflowDirectory workflowDirectory,
        [FromServices] WorkflowScheduler workflowScheduler
    )
    {
        // core: Do not use the synchronization-job here because we want to see the results immediately in the response.

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
                        next
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to synchronize workflow from '{WorkflowPath}'.", path);
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
    public async Task<IActionResult> StartNow
    (
        [FromServices] WorkflowDirectory workflowDirectory,
        [FromServices] WorkflowScheduler workflowScheduler,
        string name
    )
    {
        try
        {
            var fileName = workflowDirectory.FindFile(name, FileExtension.Json);
            var workflow = await Workflow.FromFile(fileName);
            var next = await workflowScheduler.StartNow(workflow);
            return Ok(new { name, next });
        }
        catch (WorkflowNotFoundException)
        {
            return NotFound(new { name });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to schedule workflow '{WorkflowName}'.", name);
            return Problem
            (
                detail: ex.ToString(),
                title: $"Unable to schedule workflow '{name}'.",
                statusCode: 500,
                instance: Request.Path
            );
        }
    }

    [HttpPost("[controller]/{name}:startIn")]
    public async Task<IActionResult> StartIn
    (
        [FromServices] WorkflowDirectory workflowDirectory,
        [FromServices] WorkflowScheduler workflowScheduler,
        [FromRoute] string name,
        [FromBody] StartInBody body
    )
    {
        try
        {
            var fileName = workflowDirectory.FindFile(name, FileExtension.Json);
            var workflow = await Workflow.FromFile(fileName);
            var next = await workflowScheduler.StartIn(workflow, body.Wait);
            return Ok(new { name, next });
        }
        catch (WorkflowNotFoundException)
        {
            return NotFound(new { name });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to schedule workflow '{WorkflowName}'.", name);
            return Problem
            (
                detail: ex.ToString(),
                title: $"Unable to schedule workflow '{name}'.",
                statusCode: 500,
                instance: Request.Path
            );
        }
    }

    [HttpPost("[controller]/{name}:startAt")]
    public async Task<IActionResult> StartAt
    (
        [FromServices] WorkflowDirectory workflowDirectory,
        [FromServices] WorkflowScheduler workflowScheduler,
        [FromBody] StartAtBody body,
        [FromRoute] string name
    )
    {
        try
        {
            var fileName = workflowDirectory.FindFile(name, FileExtension.Json);
            var workflow = await Workflow.FromFile(fileName);
            var next = await workflowScheduler.StartAt(workflow, body.When.FixMissingOffset().ToUniversalTime());
            return Ok(new { name, next });
        }
        catch (WorkflowNotFoundException)
        {
            return NotFound(new { name });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to schedule workflow '{WorkflowName}'.", name);
            return Problem
            (
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

    public record StartAtBody : IValidatableObject
    {
        public DateTimeOffset When { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (When.ToUniversalTime() < DateTimeOffset.UtcNow)
            {
                yield return new ValidationResult($"Workflow must start in the future.", [nameof(When)]);
            }
        }
    }
}