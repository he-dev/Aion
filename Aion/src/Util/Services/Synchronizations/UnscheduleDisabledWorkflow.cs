using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Util.Services.Synchronizations;

public class UnscheduleDisabledWorkflow
(
    ILogger<UnscheduleDisabledWorkflow> logger,
    ISchedulerFactory schedulerFactory
) : SynchronizeWorkflowAction
{
    public async IAsyncEnumerable<SynchronizationStep> Invoke(Workflow workflow)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        var canUnscheduleDisabled = !workflow.Enabled && await scheduler.CheckExists(workflow.Trigger.JobKey);

        if (canUnscheduleDisabled)
        {
            if (await scheduler.DeleteJob(workflow.Trigger.JobKey))
            {
                logger.LogInformation("Workflow '{WorkflowName}' was unscheduled.", workflow.Name);
                yield return new SynchronizationStep.DeleteJob(new { Reason = "Disabled" });
                yield break;
            }

            logger.LogWarning("Workflow '{WorkflowName}' could not be unscheduled.", workflow.Name);
        }
    }
}