using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Util.Services.Synchronizations;

public class UnscheduleEmptyWorkflow
(
    ILogger<UnscheduleEmptyWorkflow> logger,
    ISchedulerFactory schedulerFactory
) : ISynchronizeWorkflow
{
    public async Task<SynchronizeWorkflowResult?> Try(Workflow workflow)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        var canUnscheduleEmpty = workflow.Enabled && !workflow.Steps.Any();

        if (canUnscheduleEmpty)
        {
            if (await scheduler.DeleteJob(workflow.Trigger.JobKey))
            {
                logger.LogInformation("Workflow '{WorkflowName}' was unscheduled.", workflow.Name);
                return new SynchronizeWorkflowResult<UnscheduleEmptyWorkflow>(workflow.Name);
            }
            logger.LogWarning("Workflow '{WorkflowName}' could not be unscheduled.", workflow.Name);
        }

        return null;
    }
}