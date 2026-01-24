using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Util.Services.Synchronizations;

public class UnscheduleDisabledWorkflow
(
    ILogger<UnscheduleDisabledWorkflow> logger,
    ISchedulerFactory schedulerFactory
) : ISynchronizeWorkflow
{
    public async Task<SynchronizeWorkflowResult?> Try(Workflow workflow)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        var canUnscheduleDisabled = !workflow.Enabled && await scheduler.CheckExists(workflow.Trigger.JobKey);

        if (canUnscheduleDisabled)
        {
            if (await scheduler.DeleteJob(workflow.Trigger.JobKey))
            {
                logger.LogInformation("Workflow '{WorkflowName}' was unscheduled.", workflow.Name);
                return new SynchronizeWorkflowResult<UnscheduleDisabledWorkflow>(workflow.Name);
            }

            logger.LogWarning("Workflow '{WorkflowName}' could not be unscheduled.", workflow.Name);
        }

        return null;
    }
}