using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Util.Quartz;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Home.Jobs;

public class OnDemandWorkflowJob
(
    ILogger<RegularWorkflowJob> logger,
    WorkflowEngine workflowEngine
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var workflowPath = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Path))!;
        var workflowName = context.JobDetail.Key.Name;
        var workflowTrigger = context.Trigger.JobDataMap.GetEnum<WorkflowTriggerGroup>();

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
                    await workflowEngine.Start(workflow, workflowTrigger);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing workflow '{WorkflowName}'.", workflowName);
        }
    }
}