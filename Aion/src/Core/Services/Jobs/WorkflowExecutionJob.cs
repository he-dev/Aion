using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Entities;
using Aion.Util.Logging;
using Aion.Util.Quartz;
using Aion.Util.Serilog;
using Aion.Util.Services;
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

        var variables = ImmutableList<TemplateVariableGroup>.Empty.AddRange
        ([
            new InstanceVariableGroup(instanceOptions.Value.Variables) { Name = instanceOptions.Value.Name },
            new ProfileVariableGroup(profile.Variables) { Name = profileName },
            new ExecutionVariableGroup { Mode = WorkflowExecutionMode.Once },
        ]);

        var logging = profile.Logging?.ToJsonObject().RenderFilePaths(variables);
        using var profileLogging = logEventMapping.By(ProfileLogEventSignature.FromScope(), to: logging.ToLogger());

        try
        {
            var workflowMatch = profile.Workflows.Single(workflowName);
            var workflow = await workflowMatch.ToWorkflow(variables);
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