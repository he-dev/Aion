using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core;
using Aion.Core.Skills;
using Aion.Util.Quartz;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Aion.Home.Jobs;

public class ExecutesWorkflowOnDemand
(
    ILogger<ExecutesWorkflowOnSchedule> logger,
    IOptions<EngineOptions> engineOptions,
    ExecutesWorkflow executesWorkflow
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var profileName = context.JobDetail.JobDataMap.GetString(JobDataKeys.ProfileName)!;
        var profileInfo = engineOptions.Value[profileName];
        var workflowPath = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Path))!;
        var workflowName = context.JobDetail.Key.Name;
        var workflowTrigger = context.Trigger.JobDataMap.GetEnum<WorkflowTriggerGroup>();

        using var activity = new Activity("ExecuteWorkflowOnDemand").Start();
        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflowName, WorkflowTrigger = workflowTrigger });

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
            activity.Stop();
        }
        catch (Exception ex)
        {
            activity.Stop();
            logger.LogError(ex, "Error executing workflow.");
        }
    }
}