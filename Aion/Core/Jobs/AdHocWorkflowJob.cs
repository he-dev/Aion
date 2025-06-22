using System.Linq;
using System.Threading.Tasks;
using Aion.Utilities;
using Aion.Workflows;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Jobs;

public class AdHocWorkflowJob
(
    ILogger<RegularWorkflowJob> logger,
    WorkflowDirectory directory,
    WorkflowProcess process
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var workflowName = context.JobDetail.Key.Name;

        // .. The AdHoc mode executes workflows regardless of their Enabled status.
        if (await directory.FindWorkflow(workflowName) is { } workflow)
        {
            if (workflow.Steps.Any(s => s.Enabled))
            {
                var jobVariables = new VariableGroup("job")
                {
                    ["name"] = workflowName,
                    ["mode"] = context.Trigger.JobDataMap.GetString("start"),
                };

                if (context.Trigger.JobDataMap.TryGetIntValue("arguments", out var delay))
                {
                    jobVariables["delay"] = delay;
                }

                logger.LogInformation("Executing workflow '{workflow}' ad hoc.", workflowName);
                await foreach (var _ in process.Start(workflow, jobVariables)) { }
            }
            else
            {
                logger.LogWarning("Skipping workflow '{workflow}' because it has no enabled steps.", workflowName);
            }
        }
        else
        {
            logger.LogWarning("Workflow {workflow} not found.", workflowName);
        }
    }
}