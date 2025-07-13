using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Util.Quartz;
using Aion.Util.Scriban;
using Aion.Util.Serilog;
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
        var workflowTrigger = context.Trigger.JobDataMap.GetEnum<WorkflowTriggerGroup>();
        var workflowName = context.JobDetail.Key.Name;
        var workflowPath = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Path))!;

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
            logger.LogError(ex, "Canceling workflow because it could not be loaded.");
        }
    }
}