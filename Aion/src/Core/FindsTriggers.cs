using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Quartz;
using Quartz.Impl.Matchers;

namespace Aion.Core;

public class FindsTriggers(ISchedulerFactory schedulerFactory)
{
    public async IAsyncEnumerable<ITrigger> Where(GroupMatcher<JobKey> group, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobKeys = await scheduler.GetJobKeys(group, cancellationToken);
        foreach (var jobKey in jobKeys)
        {
            foreach (var trigger in await scheduler.GetTriggersOfJob(jobKey, cancellationToken))
            {
                yield return trigger;
            }
        }
    }
}