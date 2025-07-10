using System;
using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Util.Serilog;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Jobs;

public class OnDemandWorkflowJob
(
    ILogger<RegularWorkflowJob> logger,
    WorkflowProcess workflowProcess,
    WorkflowSink workflowSink
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var onDemandOption = context.Trigger.JobDataMap.GetString(nameof(OnDemandOption))!;
        var workflowName = context.JobDetail.Key.Name;
        var workflowPath = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Path))!;

        var executionId = Guid.NewGuid().ToString("D");
        using var scope = logger.BeginScopeFrom(new { WorkflowName = workflowName, ExecutionId = executionId });

        try
        {
            switch (await Workflow.FromFile(workflowPath))
            {
                case { Steps: { } steps } when steps.Any(s => s.Enabled) == false:
                    logger.LogWarning("Canceling workflow because it has no enabled steps.");
                    break;
                case var workflow:
                {
                    logger.LogInformation("Executing workflow on-demand by {OnDemandOption}.", onDemandOption);
                    var workflowVariableGroup = new WorkflowVariableGroup
                    {
                        Name = workflowName,
                        Mode = nameof(WorkflowTriggerType.OnDemand),
                        Trigger = onDemandOption,
                        JobId = executionId,
                    };
                    using var popWorkflowLogger = workflowSink.Push(workflowName, workflow.Serilog, [workflowVariableGroup]);
                    await workflowProcess.Start(workflow, workflowVariableGroup);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Canceling workflow because it could not be loaded.");
        }
    }
}