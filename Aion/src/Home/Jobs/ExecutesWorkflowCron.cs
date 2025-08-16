using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Entities;
using Aion.Core.Services;
using Aion.Util.Logging;
using Aion.Util.Quartz;
using Aion.Util.Services;
using Aion.Util.Services.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Home.Jobs;

[DisallowConcurrentExecution]
public class ExecutesWorkflowCron
(
    ILogger<ExecutesWorkflowCron> logger,
    IOptions<InstanceOptions> engineOptions,
    WorkflowScheduleRegistry workflowScheduleRegistry,
    MapsLogEvent mapsLogEvent,
    WorkflowExecution workflowExecution
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var profileName = context.Trigger.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        var workflowName = context.Trigger.JobDataMap.GetString(JobDataKeys.WorkflowName)!;
        var workflowStart = context.Trigger.JobDataMap.GetEnum<WorkflowStart>();
        var profile = engineOptions.Value[profileName];
        using var activity = new Activity($"ExecutingWorkflow{workflowStart}").Start();
        using var scope = logger.BeginScopeFrom(new
        {
            ExecutionMode = WorkflowExecutionMode.Cron,
            ProfileName = profileName,
            WorkflowName = workflowName,
            WorkflowStart = workflowStart
        });

        var variables = ImmutableList<TemplateVariableGroup>.Empty.AddRange
        ([
            new InstanceVariableGroup(engineOptions.Value.Variables) { Name = engineOptions.Value.Name },
            new ProfileVariableGroup(profile.Variables) { Name = profileName },
            new ExecutionVariableGroup { Mode = WorkflowExecutionMode.Cron },
        ]);


        try
        {
            var workflowMatch = profile.Workflows.Single(workflowName);
            var workflow = await workflowMatch.ToWorkflow(variables);
            switch (workflow)
            {
                // core: Do not execute disabled workflows.
                case { Enabled: false }:
                    logger.LogWarning("Unscheduling workflow because it is disabled.");
                    await workflowScheduleRegistry.Remove(context.JobDetail.Key);
                    break;
                // core: Do not execute workflows without any enabled steps.
                case { Steps: { } steps } when !steps.Any(s => s.Enabled):
                    logger.LogWarning("Unscheduling workflow because it has no enabled steps.");
                    await workflowScheduleRegistry.Remove(context.JobDetail.Key);
                    break;
                // core: This workflow is fine.
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