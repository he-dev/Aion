using System.Threading.Tasks;
using Aion.Core.Jobs;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Util.Services.Synchronizations;

public class ScheduleCustomWorkflow
(
    ILogger<ScheduleCustomWorkflow> logger,
    ISchedulerFactory schedulerFactory
) : ISynchronizeWorkflow
{
    public async Task<SynchronizeWorkflowResult?> Try(Workflow workflow)
    {
        var hasCustomTrigger = workflow.Trigger is not ICronTrigger;
        if (!hasCustomTrigger)
        {
            return null;
        }

        var jobDetail =
            JobBuilder
                .Create<WorkflowJob>()
                .WithIdentity(workflow.Trigger.JobKey)
                .Build();

        var scheduler = await schedulerFactory.GetScheduler();

        // core: Ensure the job is not already scheduled.
        if (await scheduler.CheckExists(jobDetail.Key))
        {
            if (await scheduler.DeleteJob(workflow.Trigger.JobKey))
            {
                logger.LogInformation("Workflow '{WorkflowName}' was unscheduled.", workflow.Name);
            }
        }

        logger.LogInformation("Workflow '{WorkflowName}' will be executed once at '{Next}'.", workflow.Name, workflow.Trigger.GetNextFireTimeUtc());

        var next = await scheduler.ScheduleJob(jobDetail, workflow.Trigger);
        return new SynchronizeWorkflowResult<ScheduleCustomWorkflow>(workflow.Name)
        {
            NextUtc = next
        };
    }
}