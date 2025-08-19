using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Entities;
using Aion.Core.Services.Mvc;
using Aion.Util;
using Aion.Util.Quartz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Home.Controllers;

[ApiController]
[Route("api/profiles/{profileName}/schedules")]
public class SchedulesController
(
    ILogger<SchedulesController> logger,
    IOptionsSnapshot<InstanceOptions> engineOptions,
    WorkflowScheduleRegistry workflowScheduleRegistry
) : ControllerBase
{
    [HttpGet]
    [ProfileExistenceValidation]
    public async Task<IActionResult> Get
    (
        string profileName,
        [FromQuery(Name = "q")] string? filter = null,
        [FromQuery] OrderBy orderBy = OrderBy.Next,
        [FromQuery] Status status = Status.Pending
    )
    {
        // var profile = engineOptions.Value[profileName];
        var utcNow = DateTimeOffset.UtcNow;

        var query =
            workflowScheduleRegistry
                .EnumerateTriggersFor(profileName)
                .Where(trigger => trigger.JobKey.Name.IsLike(filter))
                .Select(trigger => new
                {
                    name = trigger.JobKey.Name,
                    group = trigger.JobKey.Group,
                    cron = ((ICronTrigger)trigger).CronExpressionString,
                    next = ((ICronTrigger)trigger).FiresAt(utcNow).Take(3)
                });

        query = orderBy switch
        {
            OrderBy.Name => query.OrderBy(item => item.name),
            //OrderBy.Path => query.OrderBy(item => item.path),
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