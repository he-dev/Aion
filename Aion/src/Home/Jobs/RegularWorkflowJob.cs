using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Util.Scriban;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Home.Jobs;

[DisallowConcurrentExecution]
public class RegularWorkflowJob
(
    ILogger<RegularWorkflowJob> logger,
    WorkflowScheduler workflowScheduler,
    WorkflowProcess workflowProcess
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        using var activity = new Activity("ExecuteWorkflow").Start();

        var workflowName = context.JobDetail.Key.Name;
        var workflowPath = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Path))!;
        var executionId = Guid.NewGuid().ToString("D");
        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflowName, ExecutionId = executionId });

        try
        {
            switch (await Workflow.FromFile(workflowPath))
            {
                // core: Gets rid of useless workflows.
                case { Enabled: false }:
                    logger.LogWarning("Unscheduling workflow because it is disabled.");
                    await workflowScheduler.Delete(workflowName);
                    break;
                // core: Gets rid of useless workflows.
                case { Steps: { } steps } when steps.Any(s => s.Enabled) == false:
                    logger.LogWarning("Unscheduling workflow because it has no enabled steps.");
                    await workflowScheduler.Delete(workflowName);
                    break;
                // core: This is where the actual magic happens.
                case var workflow:
                    await workflowProcess.Start(workflow, ImmutableList<VariableGroup>.Empty.Add(new WorkflowVariableGroup
                    {
                        Name = workflowName,
                        Trigger = nameof(WorkflowTriggerType.Cron),
                        TraceId = activity.TraceId,
                        SpanId = activity.SpanId,
                    }));
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