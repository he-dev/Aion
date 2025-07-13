using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Home.Jobs;

[DisallowConcurrentExecution]
public class RegularWorkflowJob
(
    ILogger<RegularWorkflowJob> logger,
    WorkflowScheduler workflowScheduler,
    WorkflowEngine workflowEngine
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var workflowPath = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Path))!;
        var workflowName = context.JobDetail.Key.Name;

        try
        {
            switch (await Workflow.FromFile(workflowPath))
            {
                // core: Gets rid of useless workflows.
                case { IsOn: false }:
                    logger.LogWarning("Unscheduling workflow because it is disabled.");
                    await workflowScheduler.Delete(workflowName);
                    break;
                // core: Gets rid of useless workflows.
                case { Steps: { } steps } when steps.Any(s => s.IsOn) == false:
                    logger.LogWarning("Unscheduling workflow because it has no enabled steps.");
                    await workflowScheduler.Delete(workflowName);
                    break;
                // core: This is where the actual magic happens.
                case var workflow:
                    await workflowEngine.Start(workflow, WorkflowTriggerGroup.Cron);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unscheduling workflow because it could not be loaded.");
            if (await workflowScheduler.Delete(workflowName))
            {
                logger.LogWarning("Workflow has been unscheduled.");
            }
        }
    }
}