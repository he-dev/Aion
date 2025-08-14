using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Data;
using Aion.Core.Flow;
using Aion.Util.Flow.Scriban;
using Aion.Util.Flow.Serilog;
using Aion.Util.Logging;
using Aion.Util.Quartz;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Home.Jobs;

public class ExecutesWorkflowOnce
(
    ILogger<ExecutesWorkflowOnce> logger,
    IOptions<InstanceOptions> engineOptions,
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
            ExecutionMode = WorkflowExecutionMode.Once,
            ProfileName = profileName,
            WorkflowName = workflowName,
            WorkflowStart = workflowStart,
        });

        var variables = ImmutableList<VariableGroup>.Empty.AddRange
        ([
            new InstanceVariableGroup(engineOptions.Value.Variables) { Name = engineOptions.Value.Name },
            new ProfileVariableGroup(profile.Variables) { Name = profileName },
            new ExecutionVariableGroup { Mode = WorkflowExecutionMode.Once },
        ]);


        var logging = await RendersLogging.From(profile.Logging?.ToJsonObject(), profile.LoggingPresets, variables);
        using var profileLogging = mapsLogEvent.By(ProfileLogEventSignature.FromScope(), to: logging.ToLogger());

        try
        {
            var workflowMatch = profile.Workflows.Single(workflowName);
            var workflow = await RendersWorkflow.From(workflowMatch, variables);
            switch (workflow)
            {
                // util: Logging.
                case { Steps: { } steps } when steps.Any(s => s.Enabled) == false:
                    logger.LogWarning("Skipping workflow because it has no enabled steps.");
                    break;
                // core: This is where the actual magic happens.
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
        }
    }
}