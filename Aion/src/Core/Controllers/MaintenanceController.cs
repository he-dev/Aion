using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Core.Util.Mvc;
using Aion.Util;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Aion.Core.Controllers;

[ApiController]
[Route("api")]
public class MaintenanceController(ILogger<MaintenanceController> logger) : ControllerBase
{
    [HttpGet("[controller]")]
    public async Task<IActionResult> Get
    (
        [FromServices] WorkflowSchedule.Collection workflowSchedules,
        [FromServices] StandbyDirectory standbyDirectory
    )
    {
        // !! Get not only pending triggers, but also jobs they match.
        var pending = await standbyDirectory.ToListAsync();
        var triggers = await workflowSchedules.ToListAsync();
        var query =
            from s in pending
            orderby s.EndsOnUtc descending
            select new
            {
                s.Filter,
                s.CreatedOnUtc,
                s.StartsOnUtc,
                s.EndsOnUtc,
                s.Length,
                s.Remaining,
                triggers = triggers.Where(t => t.JobKey.Name.IsLike(s.Filter)).ToList()
            };

        return Ok(query.ToList());
    }

    [HttpPost("[controller]:startIn")]
    [ServiceFilter<EnsureWorkflowExistsAttribute>]
    public async Task<IActionResult> StartIn
    (
        [FromServices] WorkflowSchedule.Collection workflowSchedules,
        [FromServices] StandbyEngineOptions standbyOptions,
        [FromBody] StartInBody body
    )
    {
        if (!body.SkipTriggerCheck)
        {
            var triggers =
                await workflowSchedules
                    .Where(trigger => trigger.JobKey.Name.IsLike(body.Filter))
                    .ToListAsync();

            if (!triggers.Any())
            {
                return BadRequest(new
                {
                    body.Filter,
                    Message = "Adjust the filter to match a workflow or set 'SkipTriggerCheck' to true."
                });
            }
        }

        var standby = body.ToStandby().SaveTo(standbyOptions.PendingPath);
        return Ok(standby);
    }

    [HttpPost("[controller]:startAt")]
    [ServiceFilter<EnsureWorkflowExistsAttribute>]
    public async Task<IActionResult> StartAt
    (
        [FromServices] WorkflowSchedule.Collection workflowSchedules,
        [FromServices] StandbyEngineOptions standbyOptions,
        [FromBody] StartAtBody body,
        string name
    )
    {
        if (!body.SkipTriggerCheck)
        {
            var triggers =
                await workflowSchedules
                    .Where(trigger => trigger.JobKey.Name.IsLike(body.Filter))
                    .ToListAsync();

            if (!triggers.Any())
            {
                return BadRequest(new
                {
                    body.Filter,
                    Message = "Adjust the filter to match a workflow or set 'SkipTriggerCheck' to true."
                });
            }
        }

        var standby = await body.ToStandby().SaveTo(standbyOptions.PendingPath);
        return Ok(standby);
    }

    [HttpDelete("[controller]")]
    public async Task<IActionResult> Delete
    (
        [FromServices] StandbyDirectory standbyDirectory
    )
    {
        var expired = await standbyDirectory.Where(s => s.IsExpired).ToListAsync();
        foreach (var standby in expired)
        {
            await standby.Delete();
        }
        return Ok();
    }

    public record StartInBody
    {
        public bool SkipTriggerCheck { get; init; }

        public string Filter { get; init; } = null!;

        public TimeSpan Wait { get; init; }

        public TimeSpan Duration { get; init; }

        public Standby ToStandby() => Standby.Schedule(Filter, Wait, Duration);
    }

    public record StartAtBody
    {
        public bool SkipTriggerCheck { get; init; }

        public string Filter { get; init; } = null!;

        public DateTimeOffset StartsOnUtc { get; init; }

        public DateTimeOffset EndsOnUtc { get; init; }

        public Standby ToStandby() => Standby.Schedule(Filter, StartsOnUtc, EndsOnUtc);
    }
}