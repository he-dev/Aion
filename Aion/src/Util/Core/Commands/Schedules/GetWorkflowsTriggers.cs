using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Aion.Home.Jobs;
using Aion.Util.Tech.Quartz;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

namespace Aion.Util.Core.Commands.Schedules;

public class GetWorkflowsTriggers
(
    ILogger<GetWorkflowsTriggers> logger,
    ISchedulerFactory schedulerFactory
)
{
    public async IAsyncEnumerable<ITrigger> Invoke(string profileName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var groupMatcher = GroupMatcher<JobKey>.GroupEquals(new JobGroup<WorkflowJob>(profileName));

        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobKeys = await scheduler.GetJobKeys(groupMatcher, cancellationToken);
        foreach (var jobKey in jobKeys)
        {
            foreach (var trigger in await scheduler.GetTriggersOfJob(jobKey, cancellationToken))
            {
                logger.LogDebug("Found trigger '{Trigger}' for workflow '{Workflow}'.", trigger.Key, jobKey.Name);
                yield return trigger;
            }
        }
    }
}