using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Util.Services.Synchronizations;

public class RescheduleChangedWorkflow
(
    ILogger<RescheduleChangedWorkflow> logger,
    ISchedulerFactory schedulerFactory
) : ISynchronizeWorkflow
{
    public async Task<SynchronizeWorkflowResult?> Try(Workflow workflow)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        var canReschedule = workflow.Enabled && workflow.Steps.Any(s => s.Enabled);

        if (canReschedule)
        {
            // core: Compare the cron expressions.
            if (await scheduler.GetTrigger(workflow.Trigger.Key) is ICronTrigger { CronExpressionString: { } currentCron })
            {
                if (workflow.Trigger is ICronTrigger { CronExpressionString: { } otherCron } && !currentCron.Equals(otherCron))
                {
                    var next = await scheduler.RescheduleJob(workflow.Trigger.Key, workflow.Trigger);
                    logger.LogInformation("Workflow '{WorkflowName}' will be executed by {Cron} at '{Next}'.", workflow.Name, otherCron, next);
                    return new SynchronizeWorkflowResult<RescheduleChangedWorkflow>(workflow.Name)
                    {
                        NextUtc = next
                    };
                }
            }
        }

        return null;
    }
}