using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Utilities;
using Aion.Core.Workflows;
using Aion.Util;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Jobs;

public class AdHocWorkflowJob
(
    ILogger<RegularWorkflowJob> logger,
    WorkflowProcess process
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var workflowName = context.JobDetail.Key.Name;
        var root = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Root))!;
        var path = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Path))!;

        var either = await Workflow.FromFile(path, root);

        switch (either)
        {
            case Either<Workflow, Workflow.Issue>.InR { Value: var issue }:
                logger.LogError(issue.Exception, "Canceling workflow '{workflow}' because it has flaws.", workflowName);
                break;
            case Either<Workflow, Workflow.Issue>.InL { Value: var workflow } when !workflow.Steps.Any(s => s.Enabled):
                logger.LogWarning("Canceling workflow '{workflow}' because it has no enabled steps.", workflowName);
                break;
            case Either<Workflow, Workflow.Issue>.InL { Value: var workflow }:
            {
                // .. The AdHoc mode executes workflows regardless of their Enabled status.

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

                break;
            }
        }
    }
}