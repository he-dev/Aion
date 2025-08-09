using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Services;
using Aion.Core.Services.Scheduling;
using Aion.Meta.Logging;
using Aion.Util.Quartz;
using Aion.Util.Scriban;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Home.Jobs;

[DisallowConcurrentExecution]
public class ExecutesWorkflowCron
(
    ILogger<ExecutesWorkflowCron> logger,
    IOptions<InstanceOptions> engineOptions,
    CancelsWorkflowSchedule cancelsWorkflowSchedule,
    MapsLogEvent mapsLogEvent,
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
        using var scope = logger.BeginScopeFrom(new
        {
            ExecutionMode = WorkflowExecutionMode.Cron,
            ProfileName = profileName,
            WorkflowName = workflowName,
            WorkflowStart = workflowStart
        });

        var variables = ImmutableList<VariableGroup>.Empty.AddRange
        ([
            new InstanceVariableGroup(engineOptions.Value.Variables) { Name = engineOptions.Value.Name },
            new ProfileVariableGroup(profile.Variables) { Name = profileName },
            new ExecutionVariableGroup { Mode = WorkflowExecutionMode.Once },
        ]);


        //var logging = RendersLogging.From(profile.Logging);
        // var logging =
        //     profile.LoggingTemplate is not null
        //         ? await profile.LoggingTemplate.RenderAsync(profile, variables)
        //         : null;
        //
        // using var profileLogging = mapsLogEvent.By(ProfileLogEventSignature.FromScope(), to: logging.ToLogger());

        try
        {
            var workflowMatch = profile.Workflows.Single(workflowName);
            var workflow = await RendersWorkflow.From(workflowMatch, variables);
            switch (workflow)
            {
                // core: Do not execute disabled workflows.
                case { Enabled: false }:
                    logger.LogWarning("Unscheduling workflow because it is disabled.");
                    await cancelsWorkflowSchedule.Where(context.JobDetail.Key);
                    break;
                // core: Do not execute workflows without any enabled steps.
                case { Steps: { } steps } when steps.Any(s => s.Enabled) == false:
                    logger.LogWarning("Unscheduling workflow because it has no enabled steps.");
                    await cancelsWorkflowSchedule.Where(context.JobDetail.Key);
                    break;
                // core: This workflow is fine.
                default:
                    await executesWorkflow.Now(workflow);
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