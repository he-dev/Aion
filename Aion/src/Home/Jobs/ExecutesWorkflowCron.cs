using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services;
using Aion.Core.Services.Scheduling;
using Aion.Meta.Logging;
using Aion.Util.Quartz;
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
        var profileName = context.Trigger.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        var workflowName = context.Trigger.JobDataMap.GetString(JobDataKeys.WorkflowName)!;
        var workflowStart = context.Trigger.JobDataMap.GetEnum<WorkflowStart>();
        var profile = engineOptions.Value[profileName];
        using var activity = new Activity($"ExecutingWorkflow{workflowStart}").Start();
        using var scope = logger.BeginScopeFrom(new { ProfileName = profileName, WorkflowName = workflowName, WorkflowStart = workflowStart });

        try
        {
            var workflowMatch = await profile.Workflows.Single(workflowName).Load();
            switch (workflowMatch.Value)
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
                    await executesWorkflow.Now(workflowMatch);
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