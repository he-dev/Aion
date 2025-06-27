using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Util;
using Aion.Core.Util.Mvc;
using Aion.Core.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Controllers;

[ApiController]
[Route("api")]
public class MaintenanceController(ILogger<MaintenanceController> logger) : ControllerBase
{
    [HttpGet("[controller]")]
    [ServiceFilter<WorkflowMatcherAttribute>]
    public async Task<IActionResult> Get
    (
        [FromServices] WorkflowSchedule.Collection workflowSchedules,
        [FromServices] MaintenanceToken maintenanceToken
    )
    {
        // !! Get not only pending triggers, but also jobs they match.
        var pending = await maintenanceToken.Pending();
        var triggers = await workflowSchedules.ToListAsync();
        return Ok(pending.Select(p => new
        {
            p.Filter,
            p.Length,
            p.Remaining,
            p.CreatedOnUtc,
            p.ExpiresOnUtc,
            triggers = triggers.Where(t => p.Matches(t.JobKey.Name)).ToList()
        }));
    }

    [HttpPost("[controller]:startIn")]
    [ServiceFilter<EnsureWorkflowExistsAttribute>]
    public async Task<IActionResult> StartIn
    (
        [FromServices] WorkflowSchedule.Collection workflowSchedules,
        [FromServices] MaintenanceToken maintenanceToken,
        [FromBody] StartInBody body
    )
    {
        if (!tokenCookie.SkipTriggerCheck)
        {
            var triggers =
                await workflowSchedules
                    .Where(trigger => tokenCookie.Matches(trigger.JobKey.Name))
                    .ToListAsync();

            if (!triggers.Any())
            {
                return NotFound(new
                {
                    tokenCookie.Filter,
                    Message = "Adjust the filter to match a workflow or set 'SkipTriggerCheck' to true."
                });
            }
        }

        var token = await maintenanceToken.Create(tokenCookie.Filter, DateTimeOffset.UtcNow.AddMinutes(tokenCookie.DelayMinutes));
        return Ok(token);
    }

    [HttpPost("[controller]:startAt")]
    [ServiceFilter<EnsureWorkflowExistsAttribute>]
    public async Task<IActionResult> StartAt
    (
        [FromServices] WorkflowSchedule.Collection workflowSchedules,
        [FromServices] MaintenanceToken maintenanceToken,
        [FromBody] StartAtBody body,
        string name
    )
    {
        return Ok();
    }

    [HttpDelete("[controller]")]
    public async Task<IActionResult> Delete()
    {
        return Ok();
    }

    public record StartInBody : IValidatableObject
    {
        public string Filter { get; init; } = null!;

        public TimeSpan Wait { get; init; }

        public TimeSpan Duration { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Duration == TimeSpan.Zero)
            {
                yield return new ValidationResult("Maintenance must take some time.", [nameof(Duration)]);
            }
        }
    }

    public record StartAtBody : IValidatableObject
    {
        public string Filter { get; init; } = null!;

        public DateTimeOffset From { get; init; }

        public DateTimeOffset To { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (From < DateTimeOffset.UtcNow)
            {
                yield return new ValidationResult($"Maintenance must start in the future.", [nameof(From)]);
            }

            if (To < From)
            {
                yield return new ValidationResult($"Maintenance period must be positive.", [nameof(From), nameof(To)]);
            }
        }
    }
}