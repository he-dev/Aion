using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Util.Services.Synchronizations;

public class RescheduleChangedWorkflow
(
    ILogger<RescheduleChangedWorkflow> logger,
    ISchedulerFactory schedulerFactory
) : SynchronizeWorkflowAction
{
    public async IAsyncEnumerable<SynchronizationStep> Invoke(Workflow workflow)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        var canReschedule = workflow.Enabled && workflow.Steps.Any(s => s.Enabled);

        if (canReschedule)
        {
            // core: Compare cron expressions only if the workflow is already scheduled.
            if (await scheduler.GetTrigger(workflow.Trigger.Key) is ICronTrigger { CronExpressionString: { } currentCron })
            {
                // core: Compare the cron expressions.
                if (workflow.Trigger is ICronTrigger { CronExpressionString: { } otherCron } && !currentCron.Equals(otherCron))
                {
                    if (await scheduler.RescheduleJob(workflow.Trigger.Key, workflow.Trigger) is { } next)
                    {
                        logger.LogInformation("Workflow '{WorkflowName}' will be executed by {Cron} at '{Next}'.", workflow.Name, otherCron, next);
                        yield return new SynchronizationStep("RescheduleJob", next);
                    }
                    else
                    {
                        logger.LogWarning("Workflow '{WorkflowName}' could not be rescheduled.", workflow.Name);
                    }
                }
            }
        }
    }
}