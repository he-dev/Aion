using System;
using System.IO.Enumeration;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Utilities;
using Aion.Core.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace Aion.Core.Controllers;

[ApiController]
[Route("api/jobs/[controller]")]
public class MaintenanceController
(
    WorkflowSchedule.Collection workflowSchedules,
    MaintenanceToken maintenanceToken
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
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

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] MaintenanceTokenBody tokenCookie)
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

    public record MaintenanceTokenBody
    {
        public string? Filter { get; init; }
        public int DelayMinutes { get; init; }
        public bool SkipTriggerCheck { get; init; }

        public bool Matches(string value)
        {
            return Filter is null || FileSystemName.MatchesSimpleExpression(Filter, value);
        }
    }
}