using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Context.Services.Commands;
using Aion.Modules;
using Aion.Modules.Scheduler;
using Aion.Toolbox.Logging;
using Aion.Toolbox.Quartz;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Context.Jobs;

public class WorkflowJob
(
    ILogger<WorkflowJob> logger,
    ExecuteWorkflow executeWorkflow
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

        logger.LogDebug("Executing '{JobName}'.", nameof(WorkflowJob));

        try
        {
            var stepResults = await executeWorkflow.Now(profileName, workflowName);
            if (workflowMode == WorkflowMode.Cron && !stepResults.Any())
            {
                logger.LogWarning("Unscheduling workflow because it does not do anything.");
                await context.Scheduler.DeleteJob(context.JobDetail.Key);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow failed.");
        }
    }
}