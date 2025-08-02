using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services;
using Aion.Core.Services.Meta.Mvc;
using Aion.Home.Jobs;
using Aion.Util;
using Aion.Util.Quartz;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Impl.Matchers;

namespace Aion.Home.Endpoints.Schedules;

[ApiController]
[Route("api/profiles/{profileName}/schedules")]
public class ListsProfileSchedules
(
    ILogger<ListsProfileSchedules> logger,
    IOptionsSnapshot<EngineOptions> engineOptions,
    FindsTriggers findsTriggers
) : ControllerBase
{
    [HttpGet]
    [EnsuresProfileExists]
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
        var jobGroupMatcher = GroupMatcher<JobKey>.GroupEquals(JobGroupName.From<ExecutesWorkflowCron>(profileName));

        var query =
            findsTriggers
                .Where(jobGroupMatcher)
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