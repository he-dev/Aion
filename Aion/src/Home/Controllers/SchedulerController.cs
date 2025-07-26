using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Skills;
using Aion.Home.Jobs;
using Aion.Util;
using Aion.Util.Quartz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

namespace Aion.Home.Controllers;

[ApiController]
[Route("api")]
public class SchedulerController
(
    ILogger<SchedulerController> logger,
    FindsTriggers findsTriggers
) : ControllerBase
{
    [HttpGet("[controller]/jobs")]
    public async Task<IActionResult> Get
    (
        [FromQuery(Name = "q")] string? filter = null,
        [FromQuery] OrderBy orderBy = OrderBy.Next,
        [FromQuery] Status status = Status.Pending
    )
    {
        var utcNow = DateTimeOffset.UtcNow;
        var jobGroupMatcher = GroupMatcher<JobKey>.GroupStartsWith(nameof(ExecutesWorkflowOnSchedule));

        var query =
            findsTriggers
                .Where(jobGroupMatcher)
                .Where(trigger => trigger.JobKey.Name.IsLike(filter))
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