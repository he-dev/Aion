using System.Collections.Generic;
using Aion.Core.Jobs;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Util.Services.Synchronizations;

public class ScheduleCustomWorkflow
(
    ILogger<ScheduleCustomWorkflow> logger,
    ISchedulerFactory schedulerFactory
) : SynchronizeWorkflowAction
{
    public async IAsyncEnumerable<SynchronizationStep> Invoke(Workflow workflow)
    {
        var hasCustomTrigger = workflow.Trigger is not ICronTrigger;
        if (!hasCustomTrigger)
        {
            yield break;
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
                yield return new SynchronizationStep("DeleteJob");
            }
        }

        logger.LogInformation("Workflow '{WorkflowName}' will be executed once at '{Next}'.", workflow.Name, workflow.Trigger.GetNextFireTimeUtc());

        var next = await scheduler.ScheduleJob(jobDetail, workflow.Trigger);
        yield return new SynchronizationStep("ScheduleJob", next);
    }
}