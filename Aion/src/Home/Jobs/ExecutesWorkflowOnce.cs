using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Flairs;
using Aion.Util.Quartz;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Home.Jobs;

public class ExecutesWorkflowOnce
(
    ILogger<ExecutesWorkflowOnce> logger,
    IOptions<EngineOptions> engineOptions,
    ExecutesWorkflow executesWorkflow
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var profileName = context.Trigger.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        var profileInfo = engineOptions.Value[profileName];
        var workflowPath = context.Trigger.JobDataMap.GetString(JobDataKeys.WorkflowPath)!;
        var workflowName = context.Trigger.Key.Name;
        var triggerType = context.Trigger.JobDataMap.GetEnum<WorkflowTriggerType>();

        using var activity = new Activity("ExecutingWorkflowOnDemand").Start();
        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflowName, TriggerType = triggerType });

        try
        {
            switch (await Workflow.FromFile(workflowPath))
            {
                // util: Logging.
                case { Steps: { } steps } when steps.Any(s => s.IsOn) == false:
                    logger.LogWarning("Skipping workflow because it has no enabled steps.");
                    break;
                // core: This is where the actual magic happens.
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
        }
    }
}