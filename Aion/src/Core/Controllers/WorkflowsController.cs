using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Jobs;
using Aion.Core.Modules;
using Aion.Core.Util.Mvc;
using Aion.Util;
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
    public async Task<IActionResult> Get
    (
        [FromServices] WorkflowDirectory workflowDirectory,
        [FromQuery(Name = "q")] string? filter,
        [FromQuery] bool? enabled = null
    )
    {
        var utcNow = DateTimeOffset.UtcNow; // ?? Keeps the timestamp stable for all items.
        var workflows = await workflowDirectory.ClusterAsync();
        var response = new
        {
            result =
                workflows
                    .SuccessResults
                    .Where(workflow => enabled is null || workflow.Enabled == enabled) // !! Get only disabled workflows here.
                    .Where(workflow => filter is null || FileSystemName.MatchesSimpleExpression(filter, workflow.Name))
                    .Select(workflow => new
                    {
                        path = workflow.Path,
                        name = workflow.Name,
                        isOn = workflow.Enabled,
                        cron = workflow.Cron,
                        next = workflow.Trigger.FiresAt(utcNow).Take(3),
                        jobs = workflow.Steps.Count(s => s.Enabled)
                    })
                    .OrderBy(item => item.next.FirstOrDefault())
                    .ThenBy(item => item.name)
                    .ToList(),
            errors =
                workflows
                    .FailureResults
                    .Select(issue => new
                    {
                        path = issue.Path,
                        flaw = issue.Exception.ToString()
                    })
        };

        logger.LogDebug("Found {count} workflows.", response.result.Count);
        return Ok(response);
    }

    // !! The API needs to be able to synchronize workflows outside its regular schedule.
    [HttpPost("[controller]:sync")]
    public async Task<IActionResult> Synchronize
    (
        [FromServices] ISchedulerFactory schedulerFactory
    )
    {
        var scheduler = await schedulerFactory.GetScheduler();
        await scheduler.ScheduleJob(
            SynchronizationJob.CreateJobDetail(),
            TriggerBuilder.Create().StartNow().Build()
        );

        return Ok();
    }

    [HttpPost("[controller]/{name}:startNow")]
    [ServiceFilter<EnsureWorkflowExistsAttribute>]
    [ServiceFilter<EnsureWorkflowNotEmptyAttribute>]
    public async Task<IActionResult> StartNow
    (
        [FromServices] WorkflowSchedule workflowSchedule,
        string name,
        Workflow workflow
    )
    {
        var next = await workflowSchedule.StartNow(workflow);
        return Ok(new { name, next });
    }

    [HttpPost("[controller]/{name}:startIn")]
    [ServiceFilter<EnsureWorkflowExistsAttribute>]
    [ServiceFilter<EnsureWorkflowNotEmptyAttribute>]
    public async Task<IActionResult> StartIn
    (
        [FromServices] WorkflowSchedule workflowSchedule,
        [FromBody] StartInBody body,
        string name,
        Workflow workflow
    )
    {
        var next = await workflowSchedule.StartAt(workflow, DateTimeOffset.UtcNow + body.Wait);
        return Ok(new { name, next });
    }

    [HttpPost("[controller]/{name}:startAt")]
    [ServiceFilter<EnsureWorkflowExistsAttribute>]
    [ServiceFilter<EnsureWorkflowNotEmptyAttribute>]
    public async Task<IActionResult> StartAt
    (
        [FromServices] WorkflowSchedule workflowSchedule,
        [FromBody] StartAtBody body,
        string name,
        Workflow workflow
    )
    {
        var next = await workflowSchedule.StartAt(workflow, body.When);
        return Ok(new { name, next });
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