using System;
using System.Collections.Generic;
using System.Linq;
using Aion.Modules.Scheduler;
using Aion.Toolbox;
using Aion.Toolbox.Quartz;
using Quartz;

namespace Aion.Premise.Endpoints;

public static class SchedulesEndpointExtensions
{
    public static IAsyncEnumerable<object> ToResponse(this IAsyncEnumerable<ITrigger> source, string? filter = null, OrderBy orderBy = OrderBy.Next)
    {
        // meta: Keep it stable.
        var utcNow = DateTimeOffset.UtcNow;

        var query =
            source
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

        return query;
    }
}