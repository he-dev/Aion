using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Jobs;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Util.Services.Synchronizations;

public class ScheduleNewWorkflow
(
    ILogger<ScheduleNewWorkflow> logger,
    ISchedulerFactory schedulerFactory
) : ISynchronizeWorkflow
{
    public async Task<SynchronizeWorkflowResult?> Try(Workflow workflow)
    {
        var scheduler = await schedulerFactory.GetScheduler();

        var canScheduleNew =
            workflow.Enabled &&
            workflow.Steps.Any(s => s.Enabled) &&
            !await scheduler.CheckExists(workflow.Trigger.JobKey);

        if (canScheduleNew)
        {
            var jobDetail =
                JobBuilder
                    .Create<WorkflowJob>()
                    .WithIdentity(workflow.Trigger.JobKey)
                    .DisallowConcurrentExecution()
                    .Build();

            var next = await scheduler.ScheduleJob(jobDetail, workflow.Trigger);
            logger.LogInformation("Workflow '{WorkflowName}' will be executed by {Cron} at '{Next}'.", workflow.Name, ((ICronTrigger)workflow.Trigger).CronExpressionString, next);

            return new SynchronizeWorkflowResult<ScheduleNewWorkflow>(workflow.Name)
            {
                NextUtc = next
            };
        }

        return null;
    }
}