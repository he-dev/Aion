using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Options;
using Aion.Core.Quartz;
using Aion.Core.Workflows;
using Aion.Util;
using Aion.Util.Quartz;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Core.Commands.Schedules;

public class GetWorkflowSchedules
(
    ILogger<GetWorkflowSchedules> logger,
    IOptionsSnapshot<SchedulerOptions> engineOptions,
    WorkflowScheduleRegistry workflowScheduleRegistry
)
{
    public async Task<object> Invoke
    (
        string profileName,
        string? filter = null,
        OrderBy orderBy = OrderBy.Next,
        Status status = Status.Pending
    )
    {
        var utcNow = DateTimeOffset.UtcNow;

        var query =
            workflowScheduleRegistry
                .EnumerateTriggersFor(profileName)
                .Where(trigger => trigger.JobKey.Name.IsLike(filter))
                .Select(trigger => new
                {
                    workflow = trigger.JobDataMap.GetString(JobDataKeys.WorkflowName),
                    cron = ((ICronTrigger)trigger).CronExpressionString,
                    next = ((ICronTrigger)trigger).FiresAt(utcNow).Take(3)
                });

        query = orderBy switch
        {
            OrderBy.Name => query.OrderBy(item => item.workflow),
            //OrderBy.Path => query.OrderBy(item => item.path),
            OrderBy.Cron => query.OrderBy(item => item.cron),
            OrderBy.Next => query.OrderBy(item => item.next.FirstOrDefault()),
            _ => query,
        };

        var result = await query.ToListAsync(); // ?? For easier debugging.
        logger.LogDebug("Found {count} jobs.", result.Count);
        return result;
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