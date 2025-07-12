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

public class OnDemandWorkflowJob
(
    ILogger<RegularWorkflowJob> logger,
    WorkflowProcess workflowProcess
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        using var activity = new Activity("ExecuteWorkflow").Start();

        var onDemandOption = context.Trigger.JobDataMap.GetString(nameof(OnDemandOption))!;
        var workflowName = context.JobDetail.Key.Name;
        var workflowPath = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Path))!;

        var executionId = Guid.NewGuid().ToString("D");
        using var scope = logger.BeginScopeFrom(new
        {
            WorkflowName = workflowName,
            Trigger = onDemandOption,
            ExecutionId = executionId,
        });

        try
        {
            switch (await Workflow.FromFile(workflowPath))
            {
                // util: Logging.
                case { Steps: { } steps } when steps.Any(s => s.Enabled) == false:
                    logger.LogWarning("Canceling workflow because it has no enabled steps.");
                    break;
                // core: This is where the actual magic happens.
                case var workflow:
                    await workflowProcess.Start(workflow, ImmutableList<VariableGroup>.Empty.Add(new WorkflowVariableGroup
                    {
                        Name = workflowName,
                        Trigger = onDemandOption,
                        TraceId = activity.TraceId,
                        SpanId = activity.SpanId,
                    }));
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Canceling workflow because it could not be loaded.");
        }
    }
}