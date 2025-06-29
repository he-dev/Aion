using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Modules;
using Aion.Util;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Jobs;

public class AdHocWorkflowJob
(
    ILogger<RegularWorkflowJob> logger,
    WorkflowFile workflowFile,
    WorkflowExecution execution
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var workflowName = context.JobDetail.Key.Name;
        var root = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Root))!;
        var path = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Path))!;

        switch (await workflowFile.Load(path, root))
        {
            case Result<Workflow, Workflow.Issue>.Failure { Value: var issue }:
                logger.LogError(issue.Exception, "Canceling workflow '{workflow}' because it has flaws.", workflowName);
                break;
            case Result<Workflow, Workflow.Issue>.Success { Value: var workflow } when !workflow.Steps.Any(s => s.Enabled):
                logger.LogWarning("Canceling workflow '{workflow}' because it has no enabled steps.", workflowName);
                break;
            // .. The AdHoc mode executes workflows regardless of their Enabled status.
            case Result<Workflow, Workflow.Issue>.Success { Value: var workflow }:
                logger.LogInformation("Executing workflow '{workflow}' ad hoc.", workflowName);
                await execution.Start(workflow, new WorkflowVariableGroup
                {
                    Name = workflowName,
                    Mode = context.Trigger.JobDataMap.GetString("start")! // .. This is always set.
                });
                break;
        }
    }
}