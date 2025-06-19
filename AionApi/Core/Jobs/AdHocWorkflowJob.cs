using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AionApi.Utilities;
using AionApi.Workflows;
using Microsoft.Extensions.Logging;
using Quartz;

namespace AionApi.Jobs;

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

        logger.LogInformation("Executing workflow '{workflow}' ad hoc.", workflowName);

        // .. The AdHoc mode executes workflows regardless of their Enabled status.
        if (await directory.FindWorkflow(workflowName) is { } workflow)
        {
            if (workflow.Steps.Any(s => s.Enabled))
            {
                var jobVariables = new VariableGroup("job")
                {
                    ["name"] = workflowName,
                    ["start"] = context.Trigger.JobDataMap.GetString("start"),
                    ["delay"] = context.Trigger.JobDataMap.GetInt("delay"),
                };

                await foreach (var _ in process.Start(workflow, jobVariables)) { }
            }
            else
            {
                logger.LogWarning("Canceling workflow '{workflow}' because it has no enabled steps.", workflowName);
            }
        }
        else
        {
            logger.LogWarning("Workflow {workflow} not found.", workflowName);
        }
    }
}