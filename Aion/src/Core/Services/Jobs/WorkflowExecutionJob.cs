using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Entities;
using Aion.Util.Logging;
using Aion.Util.Quartz;
using Aion.Util.Serilog;
using Aion.Util.Services.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Core.Services.Jobs;

public class WorkflowExecutionJob
(
    ILogger<WorkflowExecutionJob> logger,
    IOptions<InstanceOptions> instanceOptions,
    WorkflowScheduleRegistry workflowScheduleRegistry,
    LogEventMapping logEventMapping,
    WorkflowRendering workflowRendering,
    WorkflowExecution workflowExecution
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var profileName = context.Trigger.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        var workflowName = context.Trigger.JobDataMap.GetString(JobDataKeys.WorkflowName)!;
        var executionMode = context.Trigger.JobDataMap.GetEnum<WorkflowExecutionMode>();

        var profile = instanceOptions.Value[profileName];
        using var activity = new Activity($"ExecutingWorkflow{executionMode}").Start();
        using var scope = logger.BeginScopeFrom(new
        {
            ProfileName = profileName,
            WorkflowName = workflowName,
            ExecutionMode = executionMode,
        });

        var logging = profile.RenderLogging(executionMode);
        using var profileLogging = logEventMapping.By(ProfileLogEventSignature.FromScope(), to: logging.ToLogger());

        try
        {
            var workflowMatch = profile.Workflows.Single(workflowName);
            var workflow = await workflowRendering.RenderFrom(workflowMatch);
            switch (workflow)
            {
                // core: Do not execute disabled workflows in cron mode.
                case { Enabled: false } when executionMode == WorkflowExecutionMode.Cron:
                    logger.LogWarning("Unscheduling workflow because it is disabled.");
                    await workflowScheduleRegistry.Remove(context.JobDetail.Key);
                    break;
                // core: Do not execute workflows without any enabled steps.
                case { Steps: { } steps } when !steps.Any(s => s.Enabled):
                    logger.LogWarning("Skipping workflow because it has no enabled steps.");
                    await workflowScheduleRegistry.Remove(context.JobDetail.Key);
                    break;
                // core: This is where the actual magic happens.
                default:
                    await workflowExecution.Start(workflow);
                    break;
            }

            activity.SetStatus(ActivityStatusCode.Ok).Stop();
        }
        catch (Exception ex)
        {
            activity.SetStatus(ActivityStatusCode.Error).Stop();
            logger.LogError(ex, "Error executing workflow.");
            if (await workflowScheduleRegistry.Remove(context.JobDetail.Key))
            {
                logger.LogWarning("Workflow has been unscheduled.");
            }
        }
    }
}