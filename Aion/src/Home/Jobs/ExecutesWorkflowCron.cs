using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Flairs;
using Aion.Core.Flairs.Scheduling;
using Aion.Util.Quartz;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Home.Jobs;

[DisallowConcurrentExecution]
public class ExecutesWorkflowCron
(
    ILogger<ExecutesWorkflowCron> logger,
    IOptions<EngineOptions> engineOptions,
    CancelsWorkflowSchedule cancelsWorkflowSchedule,
    ExecutesWorkflow executesWorkflow
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var profileName = context.JobDetail.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        var profileInfo = engineOptions.Value[profileName];
        var workflowPath = context.JobDetail.JobDataMap.GetString(JobDataKeys.WorkflowPath)!;
        var workflowName = context.JobDetail.Key.Name;
        var triggerType = context.Trigger.JobDataMap.GetEnum<WorkflowTriggerType>();

        using var activity = new Activity("ExecutingWorkflowOnSchedule").Start();
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName, WorkflowName = workflowName, WorkflowTrigger = triggerType });

        try
        {
            switch (await Workflow.FromFile(workflowPath))
            {
                // core: Do not execute disabled workflows.
                case { IsOn: false }:
                    logger.LogWarning("Unscheduling workflow because it is disabled.");
                    await cancelsWorkflowSchedule.Where(context.JobDetail.Key);
                    break;
                // core: Do not execute workflows without any enabled steps.
                case { Steps: { } steps } when steps.Any(s => s.IsOn) == false:
                    logger.LogWarning("Unscheduling workflow because it has no enabled steps.");
                    await cancelsWorkflowSchedule.Where(context.JobDetail.Key);
                    break;
                // core: This workflow is fine.
                case var workflow:
                    await executesWorkflow.Now(workflow, profileInfo);
                    break;
            }

            activity.SetStatus(ActivityStatusCode.Ok).Stop();
        }
        catch (Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error).Stop();
            logger.LogError(ex, "Error executing workflow.");
            if (await cancelsWorkflowSchedule.Where(context.JobDetail.Key))
            {
                logger.LogWarning("Workflow has been unscheduled.");
            }
        }
    }
}