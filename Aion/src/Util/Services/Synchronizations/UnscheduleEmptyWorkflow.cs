using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Util.Services.Synchronizations;

public class UnscheduleEmptyWorkflow
(
    ILogger<UnscheduleEmptyWorkflow> logger,
    ISchedulerFactory schedulerFactory
) : SynchronizeWorkflowAction
{
    public async IAsyncEnumerable<SynchronizationStep> Invoke(Workflow workflow)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        var canUnscheduleEmpty = workflow.Enabled && !workflow.Steps.Any();

        if (canUnscheduleEmpty)
        {
            if (await scheduler.DeleteJob(workflow.Trigger.JobKey))
            {
                logger.LogInformation("Workflow '{WorkflowName}' was unscheduled.", workflow.Name);
                yield return new SynchronizationStep("DeleteJob");
                yield break;
            }

            logger.LogWarning("Workflow '{WorkflowName}' could not be unscheduled.", workflow.Name);
        }
    }
}