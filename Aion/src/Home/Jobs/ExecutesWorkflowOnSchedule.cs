using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Core.Schedulers;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Home.Jobs;

[DisallowConcurrentExecution]
public class ExecutesWorkflowOnSchedule
(
    ILogger<ExecutesWorkflowOnSchedule> logger,
    SchedulesWorkflowExecution schedulesWorkflowExecution,
    ExecutesWorkflow executesWorkflow
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var workflowPath = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Path))!;
        var workflowName = context.JobDetail.Key.Name;

        using var activity = new Activity("ExecuteWorkflowOnSchedule").Start();
        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflowName, WorkflowTrigger = WorkflowTriggerGroup.Cron });

        try
        {
            switch (await Workflow.FromFile(workflowPath))
            {
                // core: Get rid of useless workflows.
                case { IsOn: false }:
                    logger.LogWarning("Unscheduling workflow because it is disabled.");
                    await schedulesWorkflowExecution.NoMore(context.JobDetail.Key);
                    break;
                // core: Get rid of useless workflows.
                case { Steps: { } steps } when steps.Any(s => s.IsOn) == false:
                    logger.LogWarning("Unscheduling workflow because it has no enabled steps.");
                    await schedulesWorkflowExecution.NoMore(context.JobDetail.Key);
                    break;
                // core: This is where the actual magic happens.
                case var workflow:
                    await executesWorkflow.Start(workflow);
                    break;
            }
            activity.Stop();
        }
        catch (Exception ex)
        {
            activity.Stop();
            logger.LogError(ex, "Error executing workflow.");
            if (await schedulesWorkflowExecution.NoMore(context.JobDetail.Key))
            {
                logger.LogWarning("Workflow has been unscheduled.");
            }
        }
    }
}