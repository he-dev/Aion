using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Utilities;
using Aion.Core.Workflows;
using Aion.Util;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Jobs;

[DisallowConcurrentExecution]
public class RegularWorkflowJob
(
    ILogger<RegularWorkflowJob> logger,
    MaintenanceToken maintenanceToken,
    WorkflowSchedule scheduler,
    WorkflowProcess process
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var workflowName = context.JobDetail.Key.Name;
        var pendingTokens = await maintenanceToken.Pending();
        if (pendingTokens.FirstOrDefault(t => t.Matches(workflowName)) is { } token)
        {
            logger.LogWarning
            (
                "Canceling workflow '{workflow}' because maintenance token '{token}' is pending. Remaining time: {remaining}",
                workflowName, token.Filter, token.Remaining
            );
            return;
        }

        var root = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Root))!;
        var path = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Path))!;

        var either = await Workflow.FromFile(path, root);

        switch (either)
        {
            case Either<Workflow, Workflow.Issue>.InR { Value: var issue }:
                logger.LogError(issue.Exception, "Unscheduling workflow '{workflow}' because it has flaws.", workflowName);
                await scheduler.Delete(workflowName);
                break;
            case Either<Workflow, Workflow.Issue>.InL { Value.Enabled: false }:
                logger.LogWarning("Unscheduling workflow '{workflow}' because it is disabled.", workflowName);
                await scheduler.Delete(workflowName);
                break;
            case Either<Workflow, Workflow.Issue>.InL { Value: var workflow } when !workflow.Steps.Any(s => s.Enabled):
                logger.LogWarning("Unscheduling workflow '{workflow}' because it has no enabled steps.", workflowName);
                await scheduler.Delete(workflowName);
                break;
            case Either<Workflow, Workflow.Issue>.InL { Value: var workflow }:
            {
                var jobVariables = new VariableGroup("job")
                {
                    ["name"] = workflowName,
                    ["mode"] = "cron",
                    ["cron"] = ((ICronTrigger)context.Trigger).CronExpressionString,
                };

                logger.LogInformation("Executing workflow '{workflow}' on schedule.", workflowName);
                await foreach (var _ in process.Start(workflow, jobVariables)) { }

                break;
            }
        }
    }
}