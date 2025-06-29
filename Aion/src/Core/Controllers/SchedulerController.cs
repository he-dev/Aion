using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Core.Util;
using Aion.Util.Quartz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Controllers;

[ApiController]
[Route("api")]
public class SchedulerController(ILogger<SchedulerController> logger) : ControllerBase
{
    [HttpGet("[controller]/jobs")]
    [ServiceFilter<WorkflowMatcherAttribute>]
    public async Task<IActionResult> Get
    (
        [FromServices] WorkflowSchedule.Collection workflowSchedules,
        WorkflowMatcher matcher,
        [FromQuery(Name = "q")] string? filter = null,
        [FromQuery] OrderBy orderBy = OrderBy.Next,
        [FromQuery] Status status = Status.Pending
    )
    {
        var utcNow = DateTimeOffset.UtcNow;

        var query =
            workflowSchedules
                .Where(trigger => matcher.Matches(trigger.JobKey.Name))
                .Select(trigger => new
                {
                    name = trigger.JobKey.Name,
                    path = trigger.JobDataMap.GetString(nameof(Workflow.Path))!,
                    cron = ((ICronTrigger)trigger).CronExpressionString,
                    next = ((ICronTrigger)trigger).FiresAt(utcNow).Take(3)
                });

        query = orderBy switch
        {
            OrderBy.Name => query.OrderBy(item => item.name),
            OrderBy.Path => query.OrderBy(item => item.path),
            OrderBy.Cron => query.OrderBy(item => item.cron),
            OrderBy.Next => query.OrderBy(item => item.next.FirstOrDefault()),
            _ => query,
        };

        var result = await query.ToListAsync(); // ?? For easier debugging.
        logger.LogDebug("Found {count} jobs.", result.Count);
        return Ok(result);
    }

    public enum OrderBy
    {
        Name,
        Path,
        Cron,
        Next
    }

    public enum Status
    {
        Pending,
        Running,
    }
}