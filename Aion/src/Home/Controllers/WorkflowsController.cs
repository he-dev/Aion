using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Util.Quartz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Quartz;
using NLog.Extensions.Logging;

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
                using var scope = logger.BeginScope(new { foo = "foo" });
                //using var scope = logger.BeginScope(new Dictionary<string, object> { ["foo"] = "foo" });
                logger.LogInformation("test {bar}", "bar");
                logger.LogError(ex, "Unable to load workflow '{workflow}'.", filePath);
                errors = errors.Add(new { path = filePath, exception = ex.ToString() });
            }
        }

        var utcNow = DateTimeOffset.UtcNow; // ?? Keeps the timestamp stable for all items.
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

    // !! The API needs to be able to synchronize workflows outside its regular schedule.
    [HttpPost("[controller]:sync")]
    public async Task<IActionResult> Synchronize
    (
        [FromServices] WorkflowDirectory workflowDirectory,
        [FromServices] WorkflowSchedule workflowSchedule,
        [FromServices] ISchedulerFactory schedulerFactory
    )
    {
        // !! Does not start the synchronization-job here because we want to see the results immediately.

        var result = ImmutableList<object>.Empty;
        var errors = ImmutableList<object>.Empty;

        foreach (var path in workflowDirectory.FindFiles(FileFilter.Any, FileExtension.Json))
        {
            try
            {
                if (await Workflow.FromFile(path) is { } workflow)
                {
                    var (sync, next) = await workflowSchedule.Synchronize(workflow);
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
                logger.LogError(ex, "Unable to synchronize workflow '{workflow}'.", path);
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
        [FromServices] WorkflowSchedule workflowSchedule,
        string name
    )
    {
        try
        {
            if (workflowDirectory.FindFile(name, FileExtension.Json) is { } fileName)
            {
                var workflow = await Workflow.FromFile(fileName);
                var next = await workflowSchedule.StartNow(workflow);
                return Ok(new { name, next });
            }

            return NotFound(new { name });
        }
        catch (Exception ex)
        {
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
        [FromServices] WorkflowSchedule workflowSchedule,
        [FromRoute] string name,
        [FromBody] StartInBody body
    )
    {
        try
        {
            if (workflowDirectory.FindFile(name, FileExtension.Json) is { } fileName)
            {
                var workflow = await Workflow.FromFile(fileName);
                var next = await workflowSchedule.StartIn(workflow, body.Wait);
                return Ok(new { name, next });
            }

            return NotFound(new { name });
        }
        catch (Exception ex)
        {
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
        [FromServices] WorkflowSchedule workflowSchedule,
        [FromBody] StartAtBody body,
        [FromRoute] string name
    )
    {
        try
        {
            if (workflowDirectory.FindFile(name, FileExtension.Json) is { } fileName)
            {
                var workflow = await Workflow.FromFile(fileName);
                var next = await workflowSchedule.StartAt(workflow, body.WhenUtc);
                return Ok(new { name, next });
            }

            return NotFound(new { name });
        }
        catch (Exception ex)
        {
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
        public DateTime When { get; init; }

        public string? TimeZoneId { get; init; }

        private TimeZoneInfo TimeZone =>
            string.IsNullOrEmpty(TimeZoneId)
                ? TimeZoneInfo.Local
                : TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

        public DateTimeOffset WhenUtc
        {
            get
            {
                var offset = TimeZone.GetUtcOffset(When);
                return new DateTimeOffset(When, offset).ToUniversalTime();
            }
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (WhenUtc < DateTimeOffset.UtcNow)
            {
                yield return new ValidationResult($"Workflow must start in the future.", [nameof(When)]);
            }
        }
    }
}