using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Jobs;
using Aion.Core.Modules;
using Aion.Util.Quartz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Controllers;

[ApiController]
[Route("api")]
public class WorkflowsController
(
    ILogger<WorkflowsController> logger
) : ControllerBase
{
    [HttpGet("[controller]")]
    public async Task<IActionResult> Get2
    (
        [FromServices] WorkflowDirectory workflowDirectory,
        [FromQuery(Name = "q")] string? filter,
        [FromQuery] bool? enabled = null
    )
    {
        var workflows = ImmutableList<Workflow>.Empty;
        var errors = ImmutableList<object>.Empty;
        foreach (var path in workflowDirectory.FindFiles(filter ?? FileFilter.Any, FileExtension.Json))
        {
            try
            {
                workflows = workflows.Add(await Workflow.FromFile(path));
            }
            catch (Exception ex)
            {
                errors = errors.Add(new { path, exception = ex.ToString() });
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
        [FromServices] ISchedulerFactory schedulerFactory
    )
    {
        var scheduler = await schedulerFactory.GetScheduler();
        var startsAt = await scheduler.ScheduleJob(
            SynchronizationJob.CreateJobDetail(),
            TriggerBuilder.Create().StartNow().Build()
        );

        return Ok(new { startsAt });
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
                var next = await workflowSchedule.StartAt(workflow, body.When);
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
        public DateTimeOffset When { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (When < DateTimeOffset.UtcNow)
            {
                yield return new ValidationResult($"Workflow must start in the future.", [nameof(When)]);
            }
        }
    }
}