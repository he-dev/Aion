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

public class WorkflowJob(ILogger<WorkflowJob> logger, ExecuteWorkflow executeWorkflow) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var profileName = context.Trigger.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        var workflowName = context.Trigger.JobDataMap.GetString(JobDataKeys.WorkflowName)!;
        var workflowMode = context.Trigger.JobDataMap.GetEnum<WorkflowMode>();

        using var scope = logger.BeginScopeFrom(new
        {
            JobName = context.JobDetail.Key.Name,
            ProfileName = profileName,
            WorkflowName = workflowName,
            WorkflowMode = workflowMode,
        });

        logger.LogDebug("Executing '{JobName}'.", context.JobDetail.Key.Name);

        try
        {
            var stepResults = await executeWorkflow.Now(profileName, workflowName);
            if (workflowMode == WorkflowMode.Cron && !stepResults.Any())
            {
                logger.LogWarning("Unscheduling workflow '{WorkflowName}' because it does not do anything.", workflowName);
                await context.Scheduler.DeleteJob(context.JobDetail.Key);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow '{WorkflowName}' failed.", workflowName);
            throw new JobExecutionException(ex, refireImmediately: false);
        }
    }
}