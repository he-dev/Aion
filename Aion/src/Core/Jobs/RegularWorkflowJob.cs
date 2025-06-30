using System.Linq;
using System.Threading.Tasks;
using Aion.Core.Maintenance;
using Aion.Core.Modules;
using Aion.Util;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Aion.Core.Jobs;

[DisallowConcurrentExecution]
public class RegularWorkflowJob
(
    ILogger<RegularWorkflowJob> logger,
    StandbyDirectory standbyDirectory,
    WorkflowFile workflowFile,
    WorkflowSchedule scheduler,
    WorkflowExecution execution
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var workflowName = context.JobDetail.Key.Name;

        var standbys = await standbyDirectory.Where(s => s.IsActive).ToListAsync();
        if (standbys.FirstOrDefault(s => workflowName.IsLike(s.Filter)) is { } standby)
        {
            logger.LogWarning
            (
                "Canceling workflow '{workflow}' because maintenance '{token}' is pending. Remaining time: {remaining}",
                workflowName, standby.Filter, standby.Remaining
            );
            return;
        }

        var root = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Root))!;
        var path = context.JobDetail.JobDataMap.GetString(nameof(Workflow.Path))!;

        switch (await workflowFile.Load(path, root))
        {
            case Result<Workflow, Workflow.Issue>.Failure { Value: var issue }:
                logger.LogError(issue.Exception, "Unscheduling workflow '{workflow}' because it has flaws.", workflowName);
                await scheduler.Delete(workflowName);
                break;
            case Result<Workflow, Workflow.Issue>.Success { Value.Enabled: false }:
                logger.LogWarning("Unscheduling workflow '{workflow}' because it is disabled.", workflowName);
                await scheduler.Delete(workflowName);
                break;
            case Result<Workflow, Workflow.Issue>.Success { Value: var workflow } when !workflow.Steps.Any(s => s.Enabled):
                logger.LogWarning("Unscheduling workflow '{workflow}' because it has no enabled steps.", workflowName);
                await scheduler.Delete(workflowName);
                break;
            case Result<Workflow, Workflow.Issue>.Success { Value: var workflow }:
                logger.LogInformation("Executing workflow '{workflow}' on schedule.", workflowName);
                await execution.Start(workflow, new WorkflowVariableGroup
                {
                    Name = workflowName,
                    Mode = "cron",
                    Cron = ((ICronTrigger)context.Trigger).CronExpressionString,
                });
                break;
        }
    }
}