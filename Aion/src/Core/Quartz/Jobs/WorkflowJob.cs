using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Commands.Workflows;
using Aion.Core.Workflows;
using Aion.Util.Logging;
using Aion.Util.Quartz;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Quartz.Jobs;

public class WorkflowJob
(
    ILogger<WorkflowJob> logger,
    ExecuteWorkflow executeWorkflow,
    WorkflowScheduleRegistry workflowScheduleRegistry
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var profileName = context.Trigger.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        var workflowName = context.Trigger.JobDataMap.GetString(JobDataKeys.WorkflowName)!;
        var workflowMode = context.Trigger.JobDataMap.GetEnum<WorkflowMode>();

        using var scope = logger.BeginScopeFrom(new
        {
            ProfileName = profileName,
            WorkflowName = workflowName,
            WorkflowMode = workflowMode,
        });

        try
        {
            var stepResults = await executeWorkflow.Now(profileName, workflowName, workflowMode);
            if (workflowMode == WorkflowMode.Cron && !stepResults.Any())
            {
                logger.LogWarning("Unscheduling workflow because it does not do anything.");
                await workflowScheduleRegistry.Remove(context.JobDetail.Key);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow failed.");
        }
    }
}